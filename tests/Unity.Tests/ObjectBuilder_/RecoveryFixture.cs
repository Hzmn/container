﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.Practices.Unity.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity;
using Unity.Builder;
using Unity.Builder.Strategy;
using Unity.Exceptions;

namespace Microsoft.Practices.ObjectBuilder2.Tests
{
    // Testing that the IRequiresRecovery interface is
    // properly handled in the buildup process.
    [TestClass]
    public class RecoveryFixture
    {
        [TestMethod]
        public void RecoveryIsExecutedOnException()
        {
            var recovery = new RecoveryObject();
            MockBuilderContext context = GetContext();
            context.RequiresRecovery = recovery;

            try
            {
                context.ExecuteBuildUp(new NamedTypeBuildKey<object>(), null);
            }
            catch (Exception)
            {
                // This is supposed to happen.
            }

            Assert.IsTrue(recovery.WasRecovered);
        }

        private static MockBuilderContext GetContext()
        {
            var context = new MockBuilderContext();
            context.Strategies.Add(new ThrowingStrategy());

            return context;
        }

        private class RecoveryObject : IRequiresRecovery
        {
            public bool WasRecovered;

            public void Recover()
            {
                WasRecovered = true;
            }
        }

        private class ThrowingStrategy : BuilderStrategy
        {
            public override void PreBuildUp(IBuilderContext context)
            {
                throw new Exception("Throwing from strategy");
            }
        }
    }
}
