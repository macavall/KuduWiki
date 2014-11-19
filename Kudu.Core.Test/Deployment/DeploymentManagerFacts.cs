﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test.Deployment
{
    public class DeploymentManagerFacts
    {
        [Fact]
        public void GetOrCreateStatusFileCreatesFileIfItDoesNotAlreadyExist()
        {
            // Arrange
            var changeSet = new ChangeSet("test-changeset-id", "author", "author@email.com", "commit message", DateTimeOffset.UtcNow);
            var status = new Mock<IDeploymentStatusManager>();
            var statusFile = new TestDeploymentStatusFile();
            status.Setup(s => s.Create("test-changeset-id")).Returns(statusFile).Verifiable();
            var deploymentManager = CreateDeploymentManager(status: status.Object);
            var tracer = Mock.Of<ITracer>();

            // Act
            deploymentManager.GetOrCreateStatusFile(changeSet, tracer, "test-deployer");

            // Assert
            status.Verify();
            Assert.Equal("test-deployer", statusFile.Deployer);
            Assert.Equal("author", statusFile.Author);
            Assert.Equal("author@email.com", statusFile.AuthorEmail);
            Assert.Equal("commit message", statusFile.Message);
        }

        [Fact]
        public void GetOrCreateStatusFileUpdatesFileIfItAlreadyExists()
        {
            // Arrange
            var changeSet = new ChangeSet("test-changeset-id", "author", "author@email.com", "commit message", DateTimeOffset.UtcNow);
            var status = new Mock<IDeploymentStatusManager>(MockBehavior.Strict);
            var statusFile = new TestDeploymentStatusFile();
            status.Setup(s => s.Open("test-changeset-id")).Returns(statusFile).Verifiable();
            var deploymentManager = CreateDeploymentManager(status: status.Object);
            var tracer = Mock.Of<ITracer>();

            // Act
            deploymentManager.GetOrCreateStatusFile(changeSet, tracer, "test-deployer");

            // Assert
            status.Verify();
            Assert.Equal("test-deployer", statusFile.Deployer);
            Assert.Equal("author", statusFile.Author);
            Assert.Equal("author@email.com", statusFile.AuthorEmail);
            Assert.Equal("commit message", statusFile.Message);
        }

        [Fact]
        public void InvalidCommitTextTests()
        {
            var id = Guid.NewGuid().ToString();
            var deploymentPath = @"x:\sites\deployments";
            var environment = Mock.Of<IEnvironment>(e => e.DeploymentsPath == deploymentPath);
            var analytics = Mock.Of<IAnalytics>();
            var statusLock = Mock.Of<IOperationLock>(l => l.Lock() == true);
            var stream = new Mock<MemoryStream> { CallBase = true };
            stream.Setup(s => s.Close());

            var statusFile = Path.Combine(deploymentPath, id, "status.xml");

            FileSystemHelpers.Instance = GetMockFileSystem(statusFile, () =>
            {
                stream.Object.Position = 0;
                return Encoding.UTF8.GetString(stream.Object.GetBuffer(), 0, (int)stream.Object.Length);
            });

            var fileBase = Mock.Get(FileSystemHelpers.Instance.File);
            fileBase.Setup(f => f.Create(statusFile))
                    .Returns((string path) => stream.Object);

            try
            {
                var deploymentStatus = DeploymentStatusFile.Create(id, environment, statusLock);
                deploymentStatus.Id = id;
                deploymentStatus.Status = DeployStatus.Success;
                deploymentStatus.StatusText = "Success";
                deploymentStatus.AuthorEmail = "john.doe@live.com";
                deploymentStatus.Author = "John Doe \x08";
                deploymentStatus.Message = "Invalid char is \u0010";
                deploymentStatus.Progress = String.Empty;
                deploymentStatus.EndTime = DateTime.UtcNow;
                deploymentStatus.LastSuccessEndTime = DateTime.UtcNow;
                deploymentStatus.Complete = true;
                deploymentStatus.IsTemporary = false;
                deploymentStatus.IsReadOnly = false;

                // Save
                deploymentStatus.Save();

                // Roundtrip
                deploymentStatus = DeploymentStatusFile.Open(id, environment, analytics, statusLock);

                // Assert
                Assert.Equal(id, deploymentStatus.Id);
                Assert.Equal("John Doe ?", deploymentStatus.Author);
                Assert.Equal("Invalid char is ?", deploymentStatus.Message);
            }
            finally
            {
                FileSystemHelpers.Instance = null;
            }
        }

        [Theory]
        [PropertyData("DeploymentStatusFileScenarios")]
        public void CorruptedDeploymentStatusFileTests(string content, bool expectedNull, bool expectedError)
        {
            var id = Guid.NewGuid().ToString();
            var deploymentPath = @"x:\sites\deployments";
            var environment = Mock.Of<IEnvironment>(e => e.DeploymentsPath == deploymentPath);
            var analytics = Mock.Of<IAnalytics>();
            var statusLock = Mock.Of<IOperationLock>(l => l.Lock() == true);

            var statusFile = Path.Combine(deploymentPath, id, "status.xml");

            FileSystemHelpers.Instance = GetMockFileSystem(statusFile, () => content);

            try
            {
                var status = DeploymentStatusFile.Open(id, environment, analytics, statusLock);

                if (expectedNull)
                {
                    Assert.Null(status);

                    Mock.Get(FileSystemHelpers.Instance.DirectoryInfo.FromDirectoryName(Path.Combine(deploymentPath, id)))
                        .Verify(d => d.Delete(), Times.Once());
                }
                else
                {
                    Assert.NotNull(status);

                    Mock.Get(FileSystemHelpers.Instance.DirectoryInfo.FromDirectoryName(Path.Combine(deploymentPath, id)))
                        .Verify(d => d.Delete(), Times.Never());
                }

                if (expectedError)
                {
                    Mock.Get(analytics).Verify(a => a.UnexpectedException(It.IsAny<Exception>(), true), Times.Once());
                }
                else
                {
                    Mock.Get(analytics).Verify(a => a.UnexpectedException(It.IsAny<Exception>(), It.IsAny<bool>()), Times.Never());
                }
            }
            finally
            {
                FileSystemHelpers.Instance = null;
            }
        }

        public static IEnumerable<object[]> DeploymentStatusFileScenarios
        {
            get
            {
                // happy case
                var validContent = "<?xml version=\"1.0\" encoding=\"utf-8\"" + @"?>
<deployment>
  <id>2f45d28c063eb367ee76b3c9a4b844b673ca2ad6</id>
  <author>Suwath Ch</author>
  <deployer></deployer>
  <authorEmail>suwatch@microsoft.com</authorEmail>
  <message>change 28</message>
  <progress></progress>
  <status>Success</status>
  <statusText></statusText>
  <lastSuccessEndTime>2014-11-16T01:41:39.2074382Z</lastSuccessEndTime>
  <receivedTime>2014-11-16T01:41:38.2948183Z</receivedTime>
  <startTime>2014-11-16T01:41:38.4919528Z</startTime>
  <endTime>2014-11-16T01:41:39.2074382Z</endTime>
  <complete>True</complete>
  <is_temp>False</is_temp>
  <is_readonly>False</is_readonly>
</deployment>";
                yield return new object[] { validContent, false, false };

                // missing status.xml
                string missingContent = null;
                yield return new object[] { missingContent, true, false };

                // invalid xml
                var partialContent = "<?xml version=\"1.0\" encoding=\"utf-8\"" + @"?>
<deployment>
  <id>2f45d28c063eb367ee76b3c9a4b844b673ca2ad6</id>
  <author>Suwath Ch</author>
  <deployer></deployer>
  <authorEmail>suwatch@microsoft.com</authorEmail>
  <message>change 28</message>
  <progress></progress>
  <status>Success</status>
  <statusText></statusText>
  <lastSuccessEndTime>2014-11-16T01:41:39.2074382Z</lastSuccessEndTime>
  <receivedTime>2014-11-16T01:4";
                yield return new object[] { partialContent, true, true };

                // valid xml but missing properties
                var missingProperty = "<?xml version=\"1.0\" encoding=\"utf-8\"" + @"?>
<deployment>
  <id>2f45d28c063eb367ee76b3c9a4b844b673ca2ad6</id>
  <author>Suwath Ch</author>
  <deployer></deployer>
  <authorEmail>suwatch@microsoft.com</authorEmail>
  <message>change 28</message>
  <progress></progress>
</deployment>";
                yield return new object[] { missingProperty, true, true };
            }
        }

        private IFileSystem GetMockFileSystem(string file, Func<string> content)
        {
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            var dirBase = new Mock<DirectoryBase>(MockBehavior.Strict);
            var dirInfoBase = new Mock<DirectoryInfoBase>(MockBehavior.Strict);
            var dirInfoFactory = new Mock<IDirectoryInfoFactory>(MockBehavior.Strict);

            // Setup
            fileSystem.Setup(f => f.File)
              .Returns(fileBase.Object);
            fileSystem.Setup(f => f.Directory)
              .Returns(dirBase.Object);
            fileSystem.Setup(f => f.DirectoryInfo)
              .Returns(dirInfoFactory.Object);

            fileBase.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => path == file && content() != null);
            fileBase.Setup(f => f.OpenRead(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        if (path == file)
                        {
                            return new MemoryStream(Encoding.UTF8.GetBytes(content()));
                        }

                        throw new InvalidOperationException("Should not reach here!");
                    });
            fileBase.Setup(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));

            dirInfoFactory.Setup(d => d.FromDirectoryName(It.IsAny<string>()))
                          .Returns(dirInfoBase.Object);

            dirBase.Setup(d => d.Exists(It.IsAny<string>()))
                   .Returns((string path) => path == Path.GetDirectoryName(file));

            dirInfoBase.SetupGet(d => d.Exists)
                       .Returns(true);
            dirInfoBase.SetupSet(d => d.Attributes = FileAttributes.Normal);
            dirInfoBase.Setup(d => d.GetFileSystemInfos())
                       .Returns(new FileSystemInfoBase[0]);
            dirInfoBase.Setup(d => d.Delete());

            FileSystemHelpers.Instance = fileSystem.Object;

            return fileSystem.Object;
        }

        public class TestDeploymentStatusFile : IDeploymentStatusFile
        {
            public string Id { get; set; }

            public DeployStatus Status { get; set; }

            public string StatusText { get; set; }

            public string AuthorEmail { get; set; }

            public string Author { get; set; }

            public string Message { get; set; }

            public string Progress { get; set; }

            public string Deployer { get; set; }

            public DateTime ReceivedTime { get; set; }

            public DateTime StartTime { get; set; }

            public DateTime? EndTime { get; set; }

            public DateTime? LastSuccessEndTime { get; set; }

            public bool Complete { get; set; }

            public bool IsTemporary { get; set; }

            public bool IsReadOnly { get; set; }

            public string SiteName { get; set; }

            public void Save()
            {
                // Do nothing.
            }
        }

        private static DeploymentManager CreateDeploymentManager(
                                 ISiteBuilderFactory builderFactory = null,
                                 IEnvironment environment = null,
                                 IFileSystem fileSystem = null,
                                 ITraceFactory traceFactory = null,
                                 IAnalytics analytics = null,
                                 IDeploymentSettingsManager settings = null,
                                 IDeploymentStatusManager status = null,
                                 IOperationLock deploymentLock = null,
                                 ILogger globalLogger = null,
                                 IWebHooksManager hooksManager = null)
        {
            builderFactory = builderFactory ?? Mock.Of<ISiteBuilderFactory>();
            environment = environment ?? Mock.Of<IEnvironment>();
            FileSystemHelpers.Instance = fileSystem ?? Mock.Of<IFileSystem>();
            traceFactory = traceFactory ?? Mock.Of<ITraceFactory>();
            analytics = analytics ?? Mock.Of<IAnalytics>();
            settings = settings ?? Mock.Of<IDeploymentSettingsManager>();
            status = status ?? Mock.Of<IDeploymentStatusManager>();
            deploymentLock = deploymentLock ?? Mock.Of<IOperationLock>();
            globalLogger = globalLogger ?? Mock.Of<ILogger>();

            return new DeploymentManager(builderFactory, environment, traceFactory, analytics, settings, status, deploymentLock, globalLogger, hooksManager);
        }
    }
}