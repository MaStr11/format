﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.Format;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class ConcurrentAnalyzerRunner : IAnalyzerRunner
    {
        private const string NoFormattableDocuments = "Unable to find solution when running code analysis.";

        public static IAnalyzerRunner Instance { get; } = new ConcurrentAnalyzerRunner();

        public Task<CodeAnalysisResult> RunCodeAnalysisAsync(ImmutableArray<DiagnosticAnalyzer> analyzers,
                                                             ImmutableArray<(Document Document, OptionSet OptionSet, ICodingConventionsSnapshot CodingConventions)> formattableDocuments,
                                                             ILogger logger,
                                                             CancellationToken cancellationToken)
        {
            var result = new CodeAnalysisResult();
            var solution = formattableDocuments.FirstOrDefault().Document.Project.Solution;
            var documents = formattableDocuments.Select(x => x.Document).ToList();
            Parallel.ForEach(solution.Projects, project =>
            {
                var compilation = project.GetCompilationAsync(cancellationToken).GetAwaiter().GetResult();
                // TODO: generate option set to ensure the analyzers run
                // TODO: Ensure that the coding conventions snapshop gets passed to the analyzers somehow
                var workspaceAnalyzerOptions = CodeStyleAnalyzers.GetWorkspaceAnalyzerOptions(project);
                var analyzerCompilation = compilation.WithAnalyzers(analyzers, workspaceAnalyzerOptions, cancellationToken);
                var diagnosticResult = analyzerCompilation.GetAllDiagnosticsAsync(cancellationToken).GetAwaiter().GetResult();
                foreach (var diagnostic in diagnosticResult)
                {
                    var doc = documents.Find(d => d.FilePath == diagnostic.Location.GetLineSpan().Path);
                    if (doc != null)
                    {
                        result.AddDiagnostic(doc, diagnostic);
                    }
                }
            });

            return Task.FromResult(result);
        }
    }
}
