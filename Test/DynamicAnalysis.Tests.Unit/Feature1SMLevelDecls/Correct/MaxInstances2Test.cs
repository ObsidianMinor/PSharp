﻿//-----------------------------------------------------------------------
// <copyright file="MaxInstances2Test.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.DynamicAnalysis.Tests.Unit
{
    [TestClass]
    public class MaxInstances2Test
    {
        #region tests

        [TestMethod]
        public void TestMaxInstances2()
        {
            var test = @"
using System;
using Microsoft.PSharp;

namespace AlonBug
{
    class E1 : Event {
        public E1() : base(1, -1) { }
    }

    class E2 : Event {
        public E2() : base(1, -1) { }
    }

    class E3 : Event {
        public E3() : base(-1, -1) { }
    }

    class E4 : Event { }

    class Unit : Event {
        public Unit() : base(1, -1) { }
    }

    class RealMachine : Machine
    {
        MachineId GhostMachine;

        [Start]
        [OnEntry(nameof(EntryInit))]
        [OnEventPushState(typeof(Unit), typeof(S1))]
        [OnEventGotoState(typeof(E4), typeof(S2))]
        [OnEventDoAction(typeof(E2), nameof(Action1))]
        class Init : MachineState { }

        void EntryInit()
        {
            GhostMachine = this.CreateMachine(typeof(GhostMachine), this.Id);
            this.Raise(new Unit());
        }

        [OnEntry(nameof(EntryS1))]
        class S1 : MachineState { }

        void EntryS1()
        {
            this.Send(GhostMachine, new E1());
        }

        [OnEntry(nameof(EntryS2))]
        [OnEventGotoState(typeof(Unit), typeof(S3))]
        class S2 : MachineState { }

        void EntryS2()
        {
            this.Raise(new Unit());
        }

        [OnEventGotoState(typeof(E4), typeof(S3))]
        class S3 : MachineState { }

        void Action1()
        {
            this.Assert((int)this.Payload == 100);
            this.Send(GhostMachine, new E3());
            this.Send(GhostMachine, new E3());
        }
    }

    class GhostMachine : Machine
    {
        MachineId RealMachine;

        [Start]
        [OnEntry(nameof(EntryInit))]
        [OnEventGotoState(typeof(Unit), typeof(GhostInit))]
        class Init : MachineState { }

        void EntryInit()
        {
            RealMachine = this.Payload as MachineId;
            this.Raise(new Unit());
        }

        [OnEventGotoState(typeof(E1), typeof(S1))]
        class GhostInit : MachineState { }

        [OnEntry(nameof(EntryS1))]
        [OnEventGotoState(typeof(E3), typeof(S2))]
        [IgnoreEvents(typeof(E1))]
        class S1 : MachineState { }

        void EntryS1()
        {
            this.Send(RealMachine, new E2(), 100);
        }

        [OnEntry(nameof(EntryS2))]
        [OnEventGotoState(typeof(E3), typeof(GhostInit))]
        class S2 : MachineState { }

        void EntryS2()
        {
            this.Send(RealMachine, new E4());
            this.Send(RealMachine, new E4());
            this.Send(RealMachine, new E4());
        }
    }

    public static class TestProgram
    {
        public static void Main(string[] args)
        {
            TestProgram.Execute();
            Console.ReadLine();
        }

        [EntryPoint]
        public static void Execute()
        {
            PSharpRuntime.CreateMachine(typeof(RealMachine));
        }
    }
}";

            var parser = new CSharpParser(new PSharpProject(), SyntaxFactory.ParseSyntaxTree(test), true);
            var program = parser.Parse();
            program.Rewrite();

            Configuration.SchedulingIterations = 257;
            Configuration.SchedulingStrategy = "dfs";

            var assembly = this.GetAssembly(program.GetSyntaxTree());
            AnalysisContext.Create(assembly);

            SCTEngine.Setup();
            SCTEngine.Run();

            Assert.AreEqual(0, SCTEngine.NumOfFoundBugs);
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Get assembly from the given text.
        /// </summary>
        /// <param name="tree">SyntaxTree</param>
        /// <returns>Assembly</returns>
        private Assembly GetAssembly(SyntaxTree tree)
        {
            Assembly assembly = null;
            
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Machine).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(BugFindingDispatcher).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                "PSharpTestAssembly", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    assembly = Assembly.Load(ms.ToArray());
                }
            }

            return assembly;
        }

        #endregion
    }
}
