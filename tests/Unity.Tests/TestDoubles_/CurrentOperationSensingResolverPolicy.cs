﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Unity;
using Unity.Builder;
using Unity.Policy;

namespace Microsoft.Practices.ObjectBuilder2.Tests.TestDoubles
{
    public class CurrentOperationSensingResolverPolicy<T> : IResolverPolicy
    {
        public object CurrentOperation;

        public object Resolve(IBuilderContext context)
        {
            this.CurrentOperation = context.CurrentOperation;

            return default(T);
        }
    }
}
