// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Unity.Attributes;

namespace Unity.Tests.TestObjects
{
    // A dummy class to support testing type mapping
    public class EmailService : IService, IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        public bool Disposed = false;
        public void Dispose()
        {
            Disposed = true;
        }
    }

    // A dummy class to support testing type mapping
    public class OtherEmailService : IService, IOtherService, IDisposable
    {
        public string Id = Guid.NewGuid().ToString();

        [InjectionConstructor]
        public OtherEmailService()
        {
            
        }

        public OtherEmailService(IUnityContainer container)
        {
            
        }

        public bool Disposed = false;
        public void Dispose()
        {
            Disposed = true;
        }
    }

}
