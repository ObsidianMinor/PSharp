﻿//-----------------------------------------------------------------------
// <copyright file="StaticAnalysisEngine.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;

using Microsoft.CodeAnalysis;

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// A P# static analysis engine.
    /// </summary>
    public sealed class StaticAnalysisEngine
    {
        #region fields

        /// <summary>
        /// The compilation context.
        /// </summary>
        private CompilationContext CompilationContext;

        #endregion

        #region public API

        /// <summary>
        /// Creates a P# static analysis engine.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        /// <returns>StaticAnalysisEngine</returns>
        public static StaticAnalysisEngine Create(CompilationContext context)
        {
            return new StaticAnalysisEngine(context);
        }

        /// <summary>
        /// Runs the P# static analysis engine.
        /// </summary>
        public void Run()
        {
            // Parse the projects.
            if (this.CompilationContext.Configuration.ProjectName.Equals(""))
            {
                foreach (var project in this.CompilationContext.GetSolution().Projects)
                {
                    this.AnalyzeProject(project);
                }
            }
            else
            {
                // Find the project specified by the user.
                var targetProject = this.CompilationContext.GetSolution().Projects.Where(
                    p => p.Name.Equals(this.CompilationContext.Configuration.ProjectName)).FirstOrDefault();

                var projectDependencyGraph = this.CompilationContext.GetSolution().GetProjectDependencyGraph();
                var projectDependencies = projectDependencyGraph.GetProjectsThatThisProjectTransitivelyDependsOn(targetProject.Id);

                foreach (var project in this.CompilationContext.GetSolution().Projects)
                {
                    if (!projectDependencies.Contains(project.Id) && !project.Id.Equals(targetProject.Id))
                    {
                        continue;
                    }
                    
                    this.AnalyzeProject(project);
                }
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        private StaticAnalysisEngine(CompilationContext context)
        {
            this.CompilationContext = context;
        }

        /// <summary>
        /// Analyzes the given P# project.
        /// </summary>
        /// <param name="project">Project</param>
        private void AnalyzeProject(Project project)
        {
            // Starts profiling the analysis.
            if (this.CompilationContext.Configuration.ShowRuntimeResults)
            {
                Profiler.StartMeasuringExecutionTime();
            }

            // Create a P# static analysis context.
            var context = AnalysisContext.Create(this.CompilationContext.Configuration, project);

            // Creates and runs an analysis pass that finds if a machine exposes
            // any fields or methods to other machines.
            DirectAccessAnalysisPass.Create(context).Run();

            // Creates and runs an analysis pass that computes the
            // summary for every P# machine.
            var methodSummaryAnalysis = SummarizationPass.Create(context).Run();
            if (this.CompilationContext.Configuration.ShowGivesUpInformation)
            {
                methodSummaryAnalysis.PrintGivesUpResults();
            }

            // Creates and runs an analysis pass that constructs the
            // state transition graph for each machine.
            if (this.CompilationContext.Configuration.DoStateTransitionAnalysis)
            {
                StateTransitionAnalysisPass.Create(context).Run();
            }

            // Creates and runs an analysis pass that detects if all methods
            // in each machine respect given up ownerships.
            RespectsOwnershipAnalysisPass.Create(context).Run();

            // Stops profiling the analysis.
            if (this.CompilationContext.Configuration.ShowRuntimeResults)
            {
                Profiler.StopMeasuringExecutionTime();
            }

            if (this.CompilationContext.Configuration.ShowRuntimeResults ||
                this.CompilationContext.Configuration.ShowDFARuntimeResults ||
                this.CompilationContext.Configuration.ShowROARuntimeResults)
            {
                Profiler.PrintResults();
            }
        }

        #endregion
    }
}