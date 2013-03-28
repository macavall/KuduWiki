﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.SSHKey.Test
{
    public class SSHKeyManagerFacts
    {
        private const string _privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIGpAgEAAiEAuP52TyQ82vNoHmlxc3bFZnPBBguVXwp/LX4/IAWyEUUCASUCIFT/
Sx1xg76Lgt2KZI7/OBmHRuKr8nGmemgPyMdnd9MtAhEA9wWeggZekx/tUBIZAE2J
ZQIRAL+3tm2sgZSREmYsvm992mECEHgsP0YsnLZG4ib0DCmpLhUCEQCwLEbFpXAn
p+dkzyuJC91tAhEAqZQQlB/blelwf7hrrbEOfw==
-----END RSA PRIVATE KEY-----
";
        [Fact]
        public void ConstructorThrowsIfEnvironmentIsNull()
        {
            // Arrange
            IEnvironment env = null;

            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new SSHKeyManager(env, fileSystem: null, traceFactory: null));
            Assert.Equal("environment", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsIfFileSystemIsNull()
        {
            // Arrange
            IEnvironment env = Mock.Of<IEnvironment>();
            IFileSystem fileSystem = null;

            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new SSHKeyManager(env, fileSystem, traceFactory: null));
            Assert.Equal("fileSystem", ex.ParamName);
        }

        [Fact]
        public void SetPrivateKeySetsByPassKeyCheckAndKeyIfFile()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", _privateKey))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa.pub", It.IsAny<string>()))
                    .Verifiable();

            var directory = new Mock<DirectoryBase>();
            directory.Setup(d => d.Exists(sshPath)).Returns(true).Verifiable();
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            fileBase.Verify();
        }

        [Fact]
        public void SetPrivateKeyCreatesSSHDirectoryIfItDoesNotExist()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(false).Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no")).Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", "my super secret key")).Verifiable();

            var directory = new Mock<DirectoryBase>(MockBehavior.Strict);
            directory.Setup(d => d.Exists(sshPath)).Returns(false).Verifiable();
            directory.Setup(d => d.CreateDirectory(sshPath)).Returns(Mock.Of<DirectoryInfoBase>()).Verifiable();
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            directory.Verify();
        }

        [Fact]
        public void SetPrivateKeyAllowsRepeatedInvocation()
        {
            // Arrange
            string key1 = @"-----BEGIN RSA PRIVATE KEY-----
MIICWgIBAAKBgQCRLka+LfMP4esyl2Br+PEZ+QgQk8jDMa5rXVt+Idf/M1/2RVeW
E/odUygVPmOfpHa7crCq4aXJwYjRHxaGlidKSYBzd+JI0UBx6+1S8cGwUlH7riMU
ZAUyySIXpIkEpLTjYHbYsSXzblBh7olNm1vExaMaCh4m5D3tOs/kqCDbiwIBJQKB
gFZS3fSKBiUei9jkYthqgYUQnQLwFoHmMFuDni9SZMFBJE09/LoZt0+052aTzIhv
oIshmXpcp8QSNazGYGuzOfPnKtPwJikIZnEa36YEMorlrcG+sP25coqYWA3Q1US5
XsfyXmxBib5Enx3up/JwyFyceLwZzhjPoK/etWg5zTb9AkEA7xbPft5FHWAQ98oN
n9x1TXkAo/rRMNfUSSSCf4ZSFM0emNUvVx8SKEXcZhqWP1+VcDu9IE9PmITwvUe4
m7WxVQJBAJtzEPxmvrFiortOCzOQOiV6kkly9ZKPo/QjrHRWM1gldIF3ORpZZxhz
R45UQocITcKcTxt/3B0Td54/zjbX2V8CQCBPMMv0heFgAkr/oPntW/W2Z97O3f+u
dqIZsMUf/UEUzMiLgvAY9J2ok2e+Z1SsDUaEnQRdvqXoc48zNJ9rlIECQAyaoIMq
7N3zPaB78xIExnG91IJ+8VESkMDEn0ez9lNBTqK2o8PdvEBAssZZ2+FvYEA2L+15
EdjYEJ4g2V5khz8CQQDgIy+FkYp3GWHCOjdCXG88KxiCgDa/Tz42f/9ecS/XyzK+
AP1a+ov5cqO34vONhqb7iikB5o0X8Mm0hua4HlRu
-----END RSA PRIVATE KEY-----
";

            string publicKey = @"ssh-rsa AAAAB3NzaC1yc2EAAAABJQAAAIEAkS5Gvi3zD+HrMpdga/jxGfkIEJPIwzGua11bfiHX/zNf9kVXlhP6HVMoFT5jn6R2u3KwquGlycGI0R8WhpYnSkmAc3fiSNFAcevtUvHBsFJR+64jFGQFMskiF6SJBKS042B22LEl825QYe6JTZtbxMWjGgoeJuQ97TrP5Kgg24s=";

            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"));
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()));

            var directory = new Mock<DirectoryBase>();
            directory.Setup(d => d.Exists(sshPath)).Returns(true);
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);
            fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act
            sshKeyManager.SetPrivateKey(key1);
            sshKeyManager.SetPrivateKey(_privateKey);

            // Assert
            fileBase.Verify(s => s.WriteAllText(sshPath + @"\id_rsa", It.IsAny<string>()), Times.Exactly(2));
            fileBase.Verify(s => s.WriteAllText(sshPath + @"\id_rsa.pub", publicKey));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetSSHKeyReturnsExistingKeyIfPresentOnDisk(bool ensurePublicKey)
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string expected = "my-public-key";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub")).Returns(true);
            fileBase.Setup(s => s.ReadAllText(sshPath + "\\id_rsa.pub")).Returns(expected);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetPublicKey(ensurePublicKey);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetSSHKeyNoOpsIfPublicKeyPairIsNotFoundAndEnsurePublicKeyIsNotSet()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(It.IsAny<string>()))
                    .Returns(false);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetPublicKey(ensurePublicKey: false);

            // Assert
            fileBase.Verify();
            Assert.Null(actual);
        }

        [Fact]
        public void GetSSHKeyCreatesKeyIfPublicAndPrivateKeyDoesNotAlreadyExistAndEnsurePublicKeyIsSet()
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string keyOnDisk = null;
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(It.IsAny<string>()))
                    .Returns(false);
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa.pub", It.IsAny<string>()))
                   .Callback((string name, string value) => { keyOnDisk = value; })
                   .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + "\\id_rsa", It.IsAny<string>()))
                    .Verifiable();
            fileBase.Setup(s => s.WriteAllText(sshPath + @"\config", "HOST *\r\n  StrictHostKeyChecking no"))
                    .Verifiable();

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetPublicKey(ensurePublicKey: true);

            // Assert
            fileBase.Verify();
            Assert.Equal(keyOnDisk, actual);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetSSHKeyReturnsPublicKeyIfItExists(bool ensurePublicKey)
        {
            // Arrange
            string sshPath = @"x:\path\.ssh";
            string publicKey = "this-is-my-public-key";
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            fileBase.Setup(s => s.Exists(sshPath + "\\id_rsa.pub"))
                    .Returns(true);
            fileBase.Setup(s => s.ReadAllText(sshPath + "\\id_rsa.pub"))
                    .Returns(publicKey);

            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            fileSystem.SetupGet(f => f.File).Returns(fileBase.Object);

            var environment = new Mock<IEnvironment>();
            environment.SetupGet(e => e.SSHKeyPath).Returns(sshPath);

            var sshKeyManager = new SSHKeyManager(environment.Object, fileSystem.Object, traceFactory: null);

            // Act 
            var actual = sshKeyManager.GetPublicKey(ensurePublicKey);

            // Assert
            Assert.Equal(publicKey, actual);
        }

        [Fact]
        public void GetSSHEncodedStringEncodesPublicKey()
        {
            // Arrange
            var publicKey = new RSAParameters
            {
                Exponent = new byte[] { 1, 0, 1 },
                Modulus = Convert.FromBase64String("u91Db5QwvFrtAFVuuDZQP/a4fZ12uVYYz2P8zit/A1u+o0d2ueN7orMcrkzmulfchYG64aBdjMN8JxKIeJTIbwXIq/LVLcQKq/BrPvu6HLhFFT7ZnrmHMbytHNnfJzG6MxjgIe0k2CHPsrCre20TPPZ+c3coW6PK3MHaS/cG80y1cS+FFU2HWKSlonRKgG4COcaRX8wdM1OLU2pph9tREG5frFLpqGwpdn9z4z8zEL/Wwgf26dsBSbFnU52DYjltjJKnV+B2eKiUd1u5izFFjuDyrLaRUZORF1sW4EO3jXlDpJdKtdQGqzJd6x0xCdUce0117sSAcHOilGi+n00y+Q==")
            };
            string expected = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC73UNvlDC8Wu0AVW64NlA/9rh9nXa5VhjPY/zOK38DW76jR3a543uisxyuTOa6V9yFgbrhoF2Mw3wnEoh4lMhvBcir8tUtxAqr8Gs++7ocuEUVPtmeuYcxvK0c2d8nMbozGOAh7STYIc+ysKt7bRM89n5zdyhbo8rcwdpL9wbzTLVxL4UVTYdYpKWidEqAbgI5xpFfzB0zU4tTammH21EQbl+sUumobCl2f3PjPzMQv9bCB/bp2wFJsWdTnYNiOW2MkqdX4HZ4qJR3W7mLMUWO4PKstpFRk5EXWxbgQ7eNeUOkl0q11AarMl3rHTEJ1Rx7TXXuxIBwc6KUaL6fTTL5";

            // Act
            string output = SSHEncoding.GetString(publicKey);

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void GetPEMEncodedStringEncodesPrivateKey()
        {
            // Arrange
            var privateKey = new RSAParameters
            {
                D = Convert.FromBase64String("KaWHNupm3OWSqNK9X2VFsWuCet1SM2EKnxDPGX7WBV+X0gOh2JMZViBMp/RcwQbVO2+F+/QbLMqXyDMEaWYDEAhqBeF2VPKuoHPWyxpiOxYUiqgskB7FH4QWdml2eAZp5DGL1f98JMGpb2NVqe2+Dxg92Yf7aKwjlf8OGVrKJVE="),
                DP = Convert.FromBase64String("VxvOmWBK86gMUNGMY/3Iy/n4t+XsJdbEYSIBuXuzsF2CdeMh77YJIDuLktg48IZgdWgt20GBMhcrf+XL0elGkw=="),
                DQ = Convert.FromBase64String("lVQxek9GPkVJq1V/k6+Jvfsippt9ulp+G1S6Dt20jvjgXlyTOmDYV7/f0ZIvc04gjMGtUsPYdKyYt3JFvVFNYQ=="),
                Exponent = new byte[] { 1, 0, 1 },
                InverseQ = Convert.FromBase64String("2dtFkIGZON5RWcq5dAOG42njvbzrRJUOuh2rHbq9SfTs4pKn5kIPebEQOqFxDWs005p067miumRGhLZ3boxJew=="),
                Modulus = Convert.FromBase64String("xqVRF/QIx/bAGbzkY+pVPAQ/BP1WPZ6hbWUZTryLS3OJ+rLJmWTe27xhoo/suTEUr6yOaUVeSxTg00Lvwsi1qsd1pMcZtjsB8CHkhdnsp7WxqGYIy0il9DdCMy6mv8Z80Jf0t8wahop6Klb5wRKJpxjIyIEIgxUwWuMpBuSwuH0="),
                P = Convert.FromBase64String("4w4EblF6UB7KE3dVFZOxyAIVixRFuOZf7niPjsCr0/x8SvQ3yMYa14OTsL472yKTvDIrECbmH8Adju8/hWHM/w=="),
                Q = Convert.FromBase64String("3/gqdG19aGYIuKj7Fe38tGGt5S4NEFvR0i2up+5lr9aoTZj9moFICqP+Ojkvvr5n1RSueTX3JQ5rorNmLDEugw==")
            };
            string expected = @"-----BEGIN RSA PRIVATE KEY-----
MIICXQIBAAKBgQDGpVEX9AjH9sAZvORj6lU8BD8E/VY9nqFtZRlOvItLc4n6ssmZ
ZN7bvGGij+y5MRSvrI5pRV5LFODTQu/CyLWqx3Wkxxm2OwHwIeSF2eyntbGoZgjL
SKX0N0IzLqa/xnzQl/S3zBqGinoqVvnBEomnGMjIgQiDFTBa4ykG5LC4fQIDAQAB
AoGAKaWHNupm3OWSqNK9X2VFsWuCet1SM2EKnxDPGX7WBV+X0gOh2JMZViBMp/Rc
wQbVO2+F+/QbLMqXyDMEaWYDEAhqBeF2VPKuoHPWyxpiOxYUiqgskB7FH4QWdml2
eAZp5DGL1f98JMGpb2NVqe2+Dxg92Yf7aKwjlf8OGVrKJVECQQDjDgRuUXpQHsoT
d1UVk7HIAhWLFEW45l/ueI+OwKvT/HxK9DfIxhrXg5OwvjvbIpO8MisQJuYfwB2O
7z+FYcz/AkEA3/gqdG19aGYIuKj7Fe38tGGt5S4NEFvR0i2up+5lr9aoTZj9moFI
CqP+Ojkvvr5n1RSueTX3JQ5rorNmLDEugwJAVxvOmWBK86gMUNGMY/3Iy/n4t+Xs
JdbEYSIBuXuzsF2CdeMh77YJIDuLktg48IZgdWgt20GBMhcrf+XL0elGkwJBAJVU
MXpPRj5FSatVf5Ovib37IqabfbpafhtUug7dtI744F5ckzpg2Fe/39GSL3NOIIzB
rVLD2HSsmLdyRb1RTWECQQDZ20WQgZk43lFZyrl0A4bjaeO9vOtElQ66Hasdur1J
9OzikqfmQg95sRA6oXENazTTmnTruaK6ZEaEtndujEl7
-----END RSA PRIVATE KEY-----
";

            // Act
            string output = PEMEncoding.GetString(privateKey);

            // Assert
            Assert.Equal(expected, output);
        }
    }
}
