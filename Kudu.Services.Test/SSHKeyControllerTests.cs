﻿using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SSHKey;
using Kudu.Services.SSHKey;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class SSHKeyControllerTests
    {
        [Fact]
        public void GetPublicKeyDoesNotForceRecreatePublicKeyByDefault()
        {
            // Arrange
            var sshKeyManager = new Mock<ISSHKeyManager>(MockBehavior.Strict);
            string expected = "public-key";
            sshKeyManager.Setup(s => s.GetOrCreateKey(It.Is<bool>(v => !v))).Returns(expected).Verifiable();
            var tracer = Mock.Of<ITracer>();
            var operationLock = new Mock<IOperationLock>();
            operationLock.Setup(l => l.Lock()).Returns(true);
            var controller = new SSHKeyController(tracer, sshKeyManager.Object, operationLock.Object);

            // Act
            string actual = controller.GetPublicKey();

            // Assert
            sshKeyManager.Verify();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetPublicKeyForcesRecreateIfParameterIsSet()
        {
            // Arrange
            var sshKeyManager = new Mock<ISSHKeyManager>(MockBehavior.Strict);
            string expected = "public-key";
            sshKeyManager.Setup(s => s.GetOrCreateKey(It.Is<bool>(v => v))).Returns(expected).Verifiable();
            var tracer = Mock.Of<ITracer>();
            var operationLock = new Mock<IOperationLock>();
            operationLock.Setup(l => l.Lock()).Returns(true);
            var controller = new SSHKeyController(tracer, sshKeyManager.Object, operationLock.Object);

            // Act
            string actual = controller.GetPublicKey(forceCreate: true);

            // Assert
            sshKeyManager.Verify();
            Assert.Equal(expected, actual);
        }
    }
}
