// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.


using Microsoft.Practices.Unity;
using Unity.Attributes;

namespace Unity.Tests.CollectionSupport
{
    public class TestClassWithArrayDependency
    {
        [Dependency]
        public TestClass[] Dependency { get; set; }
    }
}