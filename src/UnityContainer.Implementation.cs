﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Unity.AspectFactories;
using Unity.Builder;
using Unity.Builder.Strategy;
using Unity.Container;
using Unity.Container.Lifetime;
using Unity.Events;
using Unity.Exceptions;
using Unity.Extension;
using Unity.Lifetime;
using Unity.ObjectBuilder.BuildPlan.DynamicMethod;
using Unity.ObjectBuilder.BuildPlan.DynamicMethod.Creation;
using Unity.ObjectBuilder.BuildPlan.DynamicMethod.Method;
using Unity.ObjectBuilder.BuildPlan.DynamicMethod.Property;
using Unity.ObjectBuilder.BuildPlan.Selection;
using Unity.ObjectBuilder.Policies;
using Unity.Policy;
using Unity.Policy.BuildPlanCreator;
using Unity.Registration;
using Unity.Storage;
using Unity.Strategies;
using Unity.Strategy;

namespace Unity
{
    [CLSCompliant(true)]
    public partial class UnityContainer
    {
        #region Delegates

        private delegate object GetDelegate(Type type, string name, Type requestedType);

        internal delegate IBuilderPolicy GetPolicyDelegate(Type type, string name, Type policyInterface, out IPolicyList list);
        internal delegate void SetPolicyDelegate(Type type, string name, Type policyInterface, IBuilderPolicy policy);
        internal delegate void ClearPolicyDelegate(Type type, string name, Type policyInterface);

        #endregion


        #region Fields

        // Container specific
        private readonly UnityContainer _root;
        private readonly UnityContainer _parent;
        internal readonly LifetimeContainer _lifetimeContainer;
        private List<UnityContainerExtension> _extensions;

        // Factories

        private IList<RegistrationFactoryDelegate> _registrationFactories;


        ///////////////////////

        // Policies
        private readonly ContainerExtensionContext _extensionContext;

        // Strategies
        private StagedStrategyChain<BuilderStrategy, UnityBuildStage> _strategies;
        private StagedStrategyChain<BuilderStrategy, BuilderStage> _buildPlanStrategies;

        // Registrations
        private readonly object _syncRoot = new object();
        private HashRegistry<Type, IRegistry<string, IPolicySet>> _registrations;

        // Events
        private event EventHandler<RegisterEventArgs> Registering;
        private event EventHandler<RegisterInstanceEventArgs> RegisteringInstance;
        private event EventHandler<ChildContainerCreatedEventArgs> ChildContainerCreated;

        // Caches
        internal IStrategyChain _strategyChain;
        internal BuilderStrategy[] _buildChain;

        // Methods
        private RegisterDelegate _registerPipeline;
        internal Func<Type, string, bool> IsTypeRegistered;
        internal Func<Type, string, InternalRegistration> GetRegistration;
        internal Func<IBuilderContext, object> BuilUpPipeline;
        internal Func<INamedType, IPolicySet> Register;
        internal GetPolicyDelegate GetPolicy;
        internal SetPolicyDelegate SetPolicy;
        internal ClearPolicyDelegate ClearPolicy;

        private GetDelegate _get;

        #endregion


        #region Constructors

        /// <summary>
        /// Create a default <see cref="UnityContainer"/>.
        /// </summary>
        public UnityContainer()
        {
            // Root container
            _root = this;
            _lifetimeContainer = new LifetimeContainer(this);
            _registrations = new HashRegistry<Type, IRegistry<string, IPolicySet>>(ContainerInitialCapacity);

            // Factories
            _registrationFactories = new List<RegistrationFactoryDelegate> { Build.RegistrationAspectFactory,
                                                                           Mapping.RegistrationAspectFactory,
                                                                          Lifetime.RegistrationAspectFactory,
                                                                             Setup.RegistrationAspectFactory };
            // Context and policies
            _extensionContext = new ContainerExtensionContext(this);
            _strategies = new StagedStrategyChain<BuilderStrategy, UnityBuildStage>();
            _buildPlanStrategies = new StagedStrategyChain<BuilderStrategy, BuilderStage>();

            // Methods
            _registerPipeline = BuildRegisterPipeline();
            BuilUpPipeline = ThrowingBuildUp;
            IsTypeRegistered = (type, name) => null != Get(type, name);
            GetRegistration = GetOrAdd;
            Register = AddOrUpdate;
            GetPolicy = Get;
            SetPolicy = Set;
            ClearPolicy = Clear;
            _get = Get;

            // TODO: Initialize disposables 
            _lifetimeContainer.Add(_strategies);
            _lifetimeContainer.Add(_buildPlanStrategies);

            // Main strategy chain
            _strategies.Add(new ArrayResolveStrategy(typeof(UnityContainer).GetTypeInfo().GetDeclaredMethod(nameof(ResolveArray))), UnityBuildStage.Enumerable);
            _strategies.Add(new EnumerableResolveStrategy(typeof(UnityContainer).GetTypeInfo().GetDeclaredMethod(nameof(ResolveEnumerable))), UnityBuildStage.Enumerable);
            _strategies.Add(new BuildKeyMappingStrategy(), UnityBuildStage.TypeMapping);
            _strategies.Add(new LifetimeStrategy(), UnityBuildStage.Lifetime);
            _strategies.Add(new BuildPlanStrategy(), UnityBuildStage.Creation);

            // Build plan strategy chain
            _buildPlanStrategies.Add(new DynamicMethodConstructorStrategy(), BuilderStage.Creation);
            _buildPlanStrategies.Add(new DynamicMethodPropertySetterStrategy(), BuilderStage.Initialization);
            _buildPlanStrategies.Add(new DynamicMethodCallStrategy(), BuilderStage.Initialization);

            // Caches
            _strategyChain = new StrategyChain(_strategies);
            _buildChain = _strategies.ToArray();
            _strategies.Invalidated += OnStrategiesChanged;

            // Default Policies
            Set( null, null, GetDefaultPolicies()); 
            Set(typeof(Func<>), string.Empty, typeof(ILifetimePolicy), new PerResolveLifetimeManager());
            Set(typeof(Func<>), string.Empty, typeof(IBuildPlanPolicy), new DeferredResolveCreatorPolicy());
            Set(typeof(Lazy<>), string.Empty, typeof(IBuildPlanCreatorPolicy), new GenericLazyBuildPlanCreatorPolicy());

            // Register this instance
            RegisterInstance(typeof(IUnityContainer), null, this, new ContainerLifetimeManager());
        }

        /// <summary>
        /// Create a <see cref="Unity.UnityContainer"/> with the given parent container.
        /// </summary>
        /// <param name="parent">The parent <see cref="Unity.UnityContainer"/>. The current object
        /// will apply its own settings first, and then check the parent for additional ones.</param>
        private UnityContainer(UnityContainer parent)
        {
            // Child container initialization
            _lifetimeContainer = new LifetimeContainer(this);
            _extensionContext = new ContainerExtensionContext(this);

            // Parent
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent._lifetimeContainer.Add(this);
            _root = _parent._root;

            // Methods
            BuilUpPipeline = _parent.BuilUpPipeline;
            IsTypeRegistered = _parent.IsTypeRegistered;
            GetRegistration = _parent.GetRegistration;
            Register = CreateAndSetOrUpdate;
            GetPolicy = parent.GetPolicy;
            SetPolicy = CreateAndSetPolicy;
            ClearPolicy = delegate { };
            _get = _parent._get;

            // Strategies
            _strategies = _parent._strategies;
            _buildPlanStrategies = _parent._buildPlanStrategies;
            _strategyChain = _parent._strategyChain;
            _buildChain = _parent._buildChain;

            // Caches
            _strategies.Invalidated += OnStrategiesChanged;
        }

        #endregion


        #region Defaults

        private IPolicySet GetDefaultPolicies()
        {
            var defaults = new InternalRegistration(null, null);

            defaults.Set(typeof(IBuildPlanCreatorPolicy), new DynamicMethodBuildPlanCreatorPolicy(_buildPlanStrategies));
            defaults.Set(typeof(IConstructorSelectorPolicy), new DefaultUnityConstructorSelectorPolicy());
            defaults.Set(typeof(IPropertySelectorPolicy), new DefaultUnityPropertySelectorPolicy());
            defaults.Set(typeof(IMethodSelectorPolicy), new DefaultUnityMethodSelectorPolicy());

            return defaults;
        }

        #endregion


        #region Implementation

        private RegisterDelegate BuildRegisterPipeline()
        {
            RegisterDelegate method = null;
            foreach (RegistrationFactoryDelegate factory in _registrationFactories)
            {
                method = factory(method);
            }
            return method;
        }


        private void CreateAndSetPolicy(Type type, string name, Type policyInterface, IBuilderPolicy policy)
        {
            lock (GetRegistration)
            {
                if (null == _registrations)
                    SetupChildContainerBehaviors();
            }

            Set(type, name, policyInterface, policy);
        }

        private IPolicySet CreateAndSetOrUpdate(INamedType registration)
        {
            lock (GetRegistration)
            {
                if (null == _registrations)
                    SetupChildContainerBehaviors();
            }

            return AddOrUpdate(registration);
        }

        private void SetupChildContainerBehaviors()
        {
            _registrations = new HashRegistry<Type, IRegistry<string, IPolicySet>>(ContainerInitialCapacity);
            IsTypeRegistered = IsTypeRegisteredLocally;
            GetRegistration = (type, name) => (InternalRegistration)Get(type, name) ?? _parent.GetRegistration(type, name);
            Register = AddOrUpdate;
            GetPolicy = Get;
            SetPolicy = Set;
            ClearPolicy = Clear;
            _get = Get;
        }

        private static object ThrowingBuildUp(IBuilderContext context)
        {
            var i = -1;
            var chain = ((InternalRegistration)context.Registration).BuildChain;

            try
            {
                while (!context.BuildComplete && ++i < chain.Count)
                {
                    chain[i].PreBuildUp(context);
                }

                while (--i >= 0)
                {
                    chain[i].PostBuildUp(context);
                }
            }
            catch (Exception ex)
            {
                context.RequiresRecovery?.Recover();
                throw new ResolutionFailedException(context.OriginalBuildKey.Type,
                                                    context.OriginalBuildKey.Name, ex, context);
            }

            return context.Existing;
        }

        private static object NotThrowingBuildUp(IBuilderContext context)
        {
            var i = -1;
            var chain = ((InternalRegistration)context.Registration).BuildChain;

            try
            {
                while (!context.BuildComplete && ++i < chain.Count)
                {
                    chain[i].PreBuildUp(context);
                }

                while (--i >= 0)
                {
                    chain[i].PostBuildUp(context);
                }
            }
            catch (Exception)
            {
                return null;
            }

            return context.Existing;
        }

        private void OnStrategiesChanged(object sender, EventArgs e)
        {
            _strategyChain = new StrategyChain(_strategies);
            _buildChain = _strategies.ToArray();
        }

        private static void InstanceIsAssignable(Type assignmentTargetType, object assignmentInstance, string argumentName)
        {
            if (!(assignmentTargetType ?? throw new ArgumentNullException(nameof(assignmentTargetType)))
                .GetTypeInfo().IsAssignableFrom((assignmentInstance ?? throw new ArgumentNullException(nameof(assignmentInstance))).GetType().GetTypeInfo()))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Constants.TypesAreNotAssignable,
                        assignmentTargetType, GetTypeName(assignmentInstance)),
                    argumentName);
            }
        }

        private static string GetTypeName(object assignmentInstance)
        {
            string assignmentInstanceType;
            try
            {
                assignmentInstanceType = assignmentInstance.GetType().FullName;
            }
            catch (Exception)
            {
                assignmentInstanceType = Constants.UnknownType;
            }

            return assignmentInstanceType;
        }

        private static MiniHashSet<InternalRegistration> GetNamedRegistrations(UnityContainer container, Type type)
        {
            MiniHashSet<InternalRegistration> set;

            if (null != container._parent)
                set = GetNamedRegistrations(container._parent, type);
            else
                set = new MiniHashSet<InternalRegistration>();

            if (null == container._registrations) return set;

            var registrations = container.Get(type);
            if (null != registrations && null != registrations.Values)
            {
                var registry = registrations.Values;
                foreach (var entry in registry)
                {
                    if (entry is IContainerRegistration registration &&
                        !string.IsNullOrEmpty(registration.Name))
                        set.Add((InternalRegistration)registration);
                }
            }

            var generic = type.GetTypeInfo().IsGenericType ? type.GetGenericTypeDefinition() : type;

            if (generic != type)
            {
                registrations = container.Get(generic);
                if (null != registrations && null != registrations.Values)
                {
                    var registry = registrations.Values;
                    foreach (var entry in registry)
                    {
                        if (entry is IContainerRegistration registration &&
                            !string.IsNullOrEmpty(registration.Name))
                            set.Add((InternalRegistration)registration);
                    }
                }
            }

            return set;
        }

        private static MiniHashSet<InternalRegistration> GetNotEmptyRegistrations(UnityContainer container, Type type)
        {
            MiniHashSet<InternalRegistration> set;

            if (null != container._parent)
                set = GetNotEmptyRegistrations(container._parent, type);
            else
                set = new MiniHashSet<InternalRegistration>();

            if (null == container._registrations) return set;

            var registrations = container.Get(type);
            if (null != registrations && null != registrations.Values)
            {
                var registry = registrations.Values;
                foreach (var entry in registry)
                {
                    if (entry is IContainerRegistration registration && string.Empty != registration.Name)
                        set.Add((InternalRegistration)registration);
                }
            }

            var generic = type.GetTypeInfo().IsGenericType ? type.GetGenericTypeDefinition() : type;

            if (generic != type)
            {
                registrations = container.Get(generic);
                if (null != registrations && null != registrations.Values)
                {
                    var registry = registrations.Values;
                    foreach (var entry in registry)
                    {
                        if (entry is IContainerRegistration registration && string.Empty != registration.Name)
                            set.Add((InternalRegistration)registration);
                    }
                }
            }

            return set;
        }


        private IPolicySet CreateRegistration(Type type, string name)
        {
            RegistrationData data = new RegistrationData { Registration = new InternalRegistration(type, name) };

            _registerPipeline(this, ref data);

            return (IPolicySet)data.Registration;
        }

        private IPolicySet CreateRegistration(Type type, string name, Type policyInterface, IBuilderPolicy policy)
        {
            RegistrationData data = new RegistrationData { Registration = new InternalRegistration(type, name, policyInterface, policy) };

            _registerPipeline(this, ref data);

            return (IPolicySet)data.Registration;
        }

        #endregion


        #region Nested Types

        private class RegistrationContext : IPolicyList
        {
            private readonly InternalRegistration _registration;
            private readonly UnityContainer _container;

            internal RegistrationContext(UnityContainer container, InternalRegistration registration)
            {
                _registration = registration;
                _container = container;
            }


            #region IPolicyList

            public IBuilderPolicy Get(Type type, string name, Type policyInterface, out IPolicyList list)
            {
                if (_registration.Type != type || _registration.Name != name)
                    return _container.GetPolicy(type, name, policyInterface, out list);

                list = this;
                return (IBuilderPolicy)_registration.Get(policyInterface);
            }


            public void Set(Type type, string name, Type policyInterface, IBuilderPolicy policy)
            {
                if (_registration.Type != type || _registration.Name != name)
                    _container.SetPolicy(type, name, policyInterface, policy);
                else
                    _registration.Set(policyInterface, policy);
            }

            public void Clear(Type type, string name, Type policyInterface)
            {
                if (_registration.Type != type || _registration.Name != name)
                    _container.ClearPolicy(type, name, policyInterface);
                else
                    _registration.Clear(policyInterface);
            }

            public void ClearAll()
            {
            }

            #endregion
        }

        #endregion
    }
}
