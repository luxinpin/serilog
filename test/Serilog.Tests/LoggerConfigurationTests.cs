﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Serilog.Core;
using Serilog.Events;
using Serilog.Tests.Support;

namespace Serilog.Tests
{
    [TestFixture]
    public class LoggerConfigurationTests
    {
        class DisposableSink : ILogEventSink, IDisposable
        {
            public bool IsDisposed { get; set; }

            public void Emit(LogEvent logEvent) { }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [Test]
        public void DisposableSinksAreDisposedAlongWithRootLogger()
        {
            var sink = new DisposableSink();
            var logger = (IDisposable) new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .CreateLogger();

            logger.Dispose(); 
            Assert.IsTrue(sink.IsDisposed);
        }

        [Test]
        public void DisposableSinksAreNotDisposedAlongWithContextualLoggers()
        {
            var sink = new DisposableSink();
            var logger = (IDisposable) new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .CreateLogger()
                .ForContext<LoggerConfigurationTests>();

            logger.Dispose();
            Assert.IsFalse(sink.IsDisposed);
        }

        [Test]
        public void AFilterPreventsMatchedEventsFromPassingToTheSink()
        {
            var excluded = Some.LogEvent();
            var included = Some.LogEvent();

            var filter = new DelegateFilter(e => e.MessageTemplate != excluded.MessageTemplate);
            var events = new List<LogEvent>();
            var sink = new DelegatingSink(events.Add);
            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .Filter.With(filter)
                .CreateLogger();
            logger.Write(included);
            logger.Write(excluded);
            Assert.AreEqual(1, events.Count);
            Assert.That(events.Contains(included));
        }

// ReSharper disable UnusedMember.Local, UnusedAutoPropertyAccessor.Local
        class AB { public int A { get; set; } public int B { get; set; } }
// ReSharper restore UnusedAutoPropertyAccessor.Local, UnusedMember.Local

        [Test]
        public void SpecifyingThatATypeIsScalarCausesItToBeLoggedAsScalarEvenWhenDestructuring()
        {
            var events = new List<LogEvent>();
            var sink = new DelegatingSink(events.Add);
            
            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .Destructure.AsScalar(typeof(AB))
                .CreateLogger();

            logger.Information("{@AB}", new AB());

            var ev = events.Single();
            var prop = ev.Properties["AB"];
            Assert.IsInstanceOf<ScalarValue>(prop.Value);
        }

        [Test]
        public void TransformationsAreAppliedToEventProperties()
        {
            var events = new List<LogEvent>();
            var sink = new DelegatingSink(events.Add);

            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .Destructure.ByTransforming<AB>(ab => new { C = ab.B })
                .CreateLogger();

            logger.Information("{@AB}", new AB());

            var ev = events.Single();
            var prop = ev.Properties["AB"];
            var sv = (StructureValue)prop.Value;
            var c = sv.Properties.Single();
            Assert.AreEqual("C", c.Name);
        }
    }
}
