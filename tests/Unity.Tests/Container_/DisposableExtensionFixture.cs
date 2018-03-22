﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity;
using Unity.Extension;

namespace Microsoft.Practices.Unity.Tests
{
    [TestClass]
    public class DisposableExtensionFixture
    {
        [TestMethod]
        public void DisposableExtensionsAreDisposedWithContainerButNotRemoved()
        {
            DisposableExtension extension = new DisposableExtension();
            IUnityContainer container = new UnityContainer()
                .AddExtension(extension);

            container.Dispose();

            Assert.IsTrue(extension.Disposed);
            Assert.IsFalse(extension.Removed);
        }

        [TestMethod]
        public void OnlyDisposableExtensionAreDisposed()
        {
            DisposableExtension extension = new DisposableExtension();
            NoopExtension noop = new NoopExtension();

            IUnityContainer container = new UnityContainer()
                .AddExtension(noop)
                .AddExtension(extension);

            container.Dispose();

            Assert.IsTrue(extension.Disposed);
        }

        [TestMethod]
        public void CanSafelyDisposeContainerTwice()
        {
            DisposableExtension extension = new DisposableExtension();
            IUnityContainer container = new UnityContainer()
                .AddExtension(extension);

            container.Dispose();
            container.Dispose();
        }

        private class DisposableExtension : UnityContainerExtension, IDisposable
        {
            public bool Disposed = false;
            public bool Removed = false;

            protected override void Initialize()
            {
            }

            public override void Remove()
            {
                this.Removed = true;
            }

            public void Dispose()
            {
                if (this.Disposed)
                {
                    throw new Exception("Can't dispose twice!");
                }
                this.Disposed = true;
            }
        }

        private class NoopExtension : UnityContainerExtension
        {
            protected override void Initialize()
            {
            }
        }
    }
}
