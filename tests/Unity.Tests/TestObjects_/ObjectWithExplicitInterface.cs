﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Practices.Unity.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity.Attributes;

namespace Microsoft.Practices.Unity.Tests.TestObjects
{
    public interface ISomeCommonProperties
    {
        [Dependency]
        ILogger Logger { get; set; }

        [Dependency]
        object SyncObject { get; set; }
    }

    public class ObjectWithExplicitInterface : ISomeCommonProperties
    {
        private ILogger logger;
        private object syncObject;

        private object somethingElse;

        [Dependency]
        public object SomethingElse
        {
            get { return somethingElse; }
            set { somethingElse = value; }
        }

        [Dependency]
        ILogger ISomeCommonProperties.Logger
        {
            get { return logger; }
            set { logger = value; }
        }

        [Dependency]
        object ISomeCommonProperties.SyncObject
        {
            get { return syncObject; }
            set { syncObject = value; }
        }

        public void ValidateInterface()
        {
            Assert.IsNotNull(logger);
            Assert.IsNotNull(syncObject);
        }
    }
}
