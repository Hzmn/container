﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity.Builder;
using Unity.ObjectBuilder.Policies;
using Unity.Policy;
using Unity.Policy.Mapping;

namespace Microsoft.Practices.ObjectBuilder2.Tests
{
    [TestClass]
    public class GenericTypeMappingTest
    {
        [TestMethod]
        public void CanMapGenericTypeToNewGenericType()
        {
            var original = new NamedTypeBuildKey(typeof(IList<int>));
            var expected = new NamedTypeBuildKey(typeof(List<int>));

            IBuildKeyMappingPolicy policy = new GenericTypeBuildKeyMappingPolicy(new NamedTypeBuildKey(typeof(List<>)));

            var result = policy.Map(original, null);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CanMapGenericTypeFromNamedTypeBuildKey()
        {
            NamedTypeBuildKey original = new NamedTypeBuildKey(typeof(IList<string>), "test");
            IBuildKeyMappingPolicy policy = new GenericTypeBuildKeyMappingPolicy(new NamedTypeBuildKey(typeof(List<>), "test"));

            var result = policy.Map(original, null);

            Assert.AreEqual(typeof(List<string>), result.Type);
            Assert.AreEqual(original.Name, result.Name);
        }

        [TestMethod]
        public void PolicyThrowsIfWrongNumberOfGenericParameters()
        {
            var original = new NamedTypeBuildKey(typeof(IList<string>));
            IBuildKeyMappingPolicy policy = new GenericTypeBuildKeyMappingPolicy(new NamedTypeBuildKey(typeof(Dictionary<,>)));
            try
            {
                policy.Map(original, null);
                Assert.Fail("Expected exception");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void PolicyThrowsIfInputIsNotAGeneric()
        {
            IBuildKeyMappingPolicy policy = new GenericTypeBuildKeyMappingPolicy(new NamedTypeBuildKey(typeof(List<>)));

            try
            {
                policy.Map(new NamedTypeBuildKey<int>(), null);
                Assert.Fail("Expected Exception");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }
    }
}
