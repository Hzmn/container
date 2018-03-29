﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.Practices.ObjectBuilder2;
using Unity;
using Unity.Builder;
using Unity.Builder.Strategy;
using Unity.Extension;
using Unity.Policy;

namespace Microsoft.Practices.Unity.Tests.TestDoubles
{
    /// <summary>
    /// A simple extension that puts the supplied strategy into the
    /// chain at the indicated stage.
    /// </summary>
    internal class SpyExtension : UnityContainerExtension
    {
        private BuilderStrategy strategy;
        private UnityBuildStage stage;
        private IBuilderPolicy policy;
        private Type policyType;

        public SpyExtension(BuilderStrategy strategy, UnityBuildStage stage)
        {
            this.strategy = strategy;
            this.stage = stage;
        }

        public SpyExtension(BuilderStrategy strategy, UnityBuildStage stage, IBuilderPolicy policy, Type policyType)
        {
            this.strategy = strategy;
            this.stage = stage;
            this.policy = policy;
            this.policyType = policyType;
        }

        protected override void Initialize()
        {
            // TODO: Context.Strategies.Add(this.strategy, this.stage);
            Context.Policies.Set(null, null, this.policyType, this.policy);
        }
    }
}
