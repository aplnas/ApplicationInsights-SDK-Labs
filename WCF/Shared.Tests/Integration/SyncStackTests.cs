﻿using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Wcf.Tests.Channels;
using Microsoft.ApplicationInsights.Wcf.Tests.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace Microsoft.ApplicationInsights.Wcf.Tests.Integration
{
    [TestClass]
    public class FullStackTests
    {
        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void TelemetryEventsAreGeneratedOnServiceCall()
        {
            TestTelemetryChannel.Clear();
            using ( var host = new HostingContext<SimpleService, ISimpleService>() )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                client.GetSimpleData();
                Assert.IsTrue(TestTelemetryChannel.CollectedData().Count > 0);
            }
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void OperationNameIsSetBasedOnOperationCalled()
        {
            TestTelemetryChannel.Clear();
            using ( var host = new HostingContext<SimpleService, ISimpleService>() )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                client.GetSimpleData();
            }
            var operationName = TestTelemetryChannel.CollectedData()
                               .Select(x => x.Context.Operation.Name)
                               .First();

            Assert.IsTrue(operationName.EndsWith("GetSimpleData", StringComparison.Ordinal));
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void AllTelemetryEventsFromOneCallHaveSameOperationId()
        {
            TestTelemetryChannel.Clear();
            using ( var host = new HostingContext<SimpleService, ISimpleService>() )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                client.GetSimpleData();
            }
            var ids = TestTelemetryChannel.CollectedData()
                    .Select(x => x.Context.Operation.Id)
                    .Distinct();

            Assert.AreEqual(1, ids.Count());
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void CallWithUnknownActionReportsCatchAllOperation()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>();
            using ( host )
            {
                host.Open();

                ISimpleService client = host.GetChannel();
                using ( OperationContextScope scope = new OperationContextScope((IContextChannel)client) )
                {
                    OperationContext.Current.OutgoingMessageHeaders.Action = "http://someaction";
                    client.CatchAllOperation();
                }
            }
            var evt = TestTelemetryChannel.CollectedData().First();
            Assert.AreEqual("ISimpleService.CatchAllOperation", evt.Context.Operation.Name);
        }


        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void ErrorTelemetryEventsAreGeneratedOnFault()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>()
                      .ShouldWaitForCompletion();
            using ( host )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                try
                {
                    client.CallFailsWithFault();
                } catch
                {
                }
            }
            var errors = from item in TestTelemetryChannel.CollectedData()
                         where item is ExceptionTelemetry
                         select item;
            Assert.IsTrue(errors.Count() > 0);
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void ErrorTelemetryEventsContainDetailedInfo()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>()
                      .ShouldWaitForCompletion();
            using ( host )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                try
                {
                    client.CallFailsWithFault();
                } catch
                {
                }
            }
            var error = (from item in TestTelemetryChannel.CollectedData()
                         where item is ExceptionTelemetry
                         select item).Cast<ExceptionTelemetry>().First();
            Assert.IsNotNull(error.Exception);
            Assert.IsNotNull(error.Context.Operation.Id);
            Assert.IsNotNull(error.Context.Operation.Name);
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void ErrorTelemetryEventsContainDetailedInfoOnTypedFault()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>()
                      .ShouldWaitForCompletion();
            using ( host )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                try
                {
                    client.CallFailsWithTypedFault();
                } catch
                {
                }
            }
            var error = (from item in TestTelemetryChannel.CollectedData()
                         where item is ExceptionTelemetry
                         select item).Cast<ExceptionTelemetry>().First();
            Assert.IsNotNull(error.Exception);
            Assert.IsNotNull(error.Context.Operation.Id);
            Assert.IsNotNull(error.Context.Operation.Name);
        }


        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void ErrorTelemetryEventsAreGeneratedOnExceptionAndIEDIF_False()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>()
                      .ShouldWaitForCompletion();
            using ( host )
            {
                host.Open();
                ISimpleService client = host.GetChannel();
                try
                {
                    client.CallFailsWithException();
                } catch
                {
                }
            }
            var errors = from item in TestTelemetryChannel.CollectedData()
                         where item is ExceptionTelemetry
                         select item;
            Assert.IsTrue(errors.Count() > 0);
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("Sync")]
        public void ErrorTelemetryEventsAreGeneratedOnExceptionAndIEDIF_True()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SimpleService, ISimpleService>()
                      .ShouldWaitForCompletion()
                      .IncludeDetailsInFaults();
            using ( host )
            {
                host.Open();

                ISimpleService client = host.GetChannel();
                try
                {
                    client.CallFailsWithException();
                } catch
                {
                }
            }
            var errors = from item in TestTelemetryChannel.CollectedData()
                         where item is ExceptionTelemetry
                         select item;
            Assert.IsTrue(errors.Count() > 0);
        }



        [TestMethod]
        [TestCategory("Integration"), TestCategory("OperationTelemetry")]
        public void CallsToOpMarkedWithOperationTelemetryGeneratesEvents()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SelectiveTelemetryService, ISelectiveTelemetryService>();
            using ( host )
            {
                host.Open();

                ISelectiveTelemetryService client = host.GetChannel();
                client.OperationWithTelemetry();
            }
            Assert.IsTrue(TestTelemetryChannel.CollectedData().Count > 0);
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("OperationTelemetry")]
        public void CallsToOpWithoutOperationTelemetryGeneratesEvents()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SelectiveTelemetryService, ISelectiveTelemetryService>();
            using ( host )
            {
                host.Open();

                ISelectiveTelemetryService client = host.GetChannel();
                client.OperationWithoutTelemetry();
            }
            Assert.AreEqual(0, TestTelemetryChannel.CollectedData().Count);
        }

        [TestMethod]
        [TestCategory("Integration"), TestCategory("OperationTelemetry")]
        public void CallCanFlowRootOperationId()
        {
            TestTelemetryChannel.Clear();
            var host = new HostingContext<SelectiveTelemetryService, ISelectiveTelemetryService>();
            using ( host )
            {
                host.Open();

                ISelectiveTelemetryService client = host.GetChannel();
                using ( var scope = new OperationContextScope((IContextChannel)client) )
                {
                    var rootId = new RootIdMessageHeader();
                    rootId.RootId = "rootId";
                    OperationContext.Current.OutgoingMessageHeaders.Add(rootId);
                    client.OperationWithTelemetry();
                }
            }
            Assert.AreEqual("rootId", TestTelemetryChannel.CollectedData().First().Context.Operation.Id);
        }

        public class RootIdMessageHeader : MessageHeader
        {
            public override string Name {
                get { return "requestRootId"; }
            }

            public override string Namespace {
                get { return "http://schemas.microsoft.com/application-insights"; }
            }

            public String RootId { get; set; }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteString(RootId);
            }
        }

    }
}
