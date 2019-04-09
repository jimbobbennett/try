﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Try.Project.Execution;
using Microsoft.DotNet.Try.Protocol.Execution;
using Workspace = Microsoft.DotNet.Try.Protocol.Execution.Workspace;

namespace Microsoft.DotNet.Try.Project.Extensions
{
    public static class FileExtensions
    {
        public static SourceFile ToSourceFile(this Workspace.File file)
        {
            return SourceFile.Create(file.Text, file.Name);
        }

        public static IEnumerable<Viewport> ExtractViewPorts(this Workspace.File file)
        {
            return file.ToSourceFile().ExtractViewPorts();
        }

        public static IEnumerable<Viewport> ExtractViewPorts(this SourceFile sourceFile)
        {
            
            var code = sourceFile.Text;
            var fileName = sourceFile.Name;
            var regions = ExtractRegions(code, fileName);

            foreach (var region in regions)
            {
                yield return new Viewport(sourceFile, region.span, region.bufferId);
            }
        }

        public static IEnumerable<Viewport> ExtractViewports(this IEnumerable<Workspace.File> files)
        {
            return files.Select(f => f.ToSourceFile()).ExtractViewports();
        }

        public static IEnumerable<Viewport> ExtractViewports(this IEnumerable<SourceFile> sourceFiles)
        {
            return sourceFiles.SelectMany(f => f.ExtractViewPorts());
        }

        private static IEnumerable<(BufferId bufferId, TextSpan span)> ExtractRegions(SourceText code, string fileName)
        {
            var ids = new HashSet<string>();
            IEnumerable<(SyntaxTrivia startRegion, SyntaxTrivia endRegion, BufferId bufferId)> FindRegions(SyntaxNode syntaxNode)
            {
                var nodesWithRegionDirectives =
                    from node in syntaxNode.DescendantNodesAndTokens()
                    where node.HasLeadingTrivia
                    from leadingTrivia in node.GetLeadingTrivia()
                    where leadingTrivia.Kind() == SyntaxKind.RegionDirectiveTrivia ||
                          leadingTrivia.Kind() == SyntaxKind.EndRegionDirectiveTrivia
                    select node;

                var stack = new Stack<SyntaxTrivia>();
                var processedSpans = new HashSet<TextSpan>();

                foreach (var nodeWithRegionDirective in nodesWithRegionDirectives)
                {
                    var triviaList = nodeWithRegionDirective.GetLeadingTrivia();

                    foreach (var currentTrivia in triviaList)
                    {
                        if (processedSpans.Add(currentTrivia.FullSpan))
                        {
                            if (currentTrivia.Kind() == SyntaxKind.RegionDirectiveTrivia)
                            {
                                stack.Push(currentTrivia);
                            }
                            else if (currentTrivia.Kind() == SyntaxKind.EndRegionDirectiveTrivia && stack.Count > 0)
                            {
                                var start = stack.Pop();
                                var regionName = start.ToFullString().Replace("#region", string.Empty).Trim();
                                yield return (start, currentTrivia, new BufferId(fileName, regionName));
                            }
                        }
                    }
                }
            }

            var sourceCodeText = code.ToString();
            var root = CSharpSyntaxTree.ParseText(sourceCodeText).GetRoot();

            foreach (var (startRegion, endRegion, label) in FindRegions(root))
            {
                var start = startRegion.GetLocation().SourceSpan.End;
                var length = endRegion.GetLocation().SourceSpan.Start -
                             startRegion.GetLocation().SourceSpan.End;
                var loc = new TextSpan(start, length);
                if (!ids.Add(label.RegionName))
                {
                    throw new InvalidOperationException("viewports identifiers must be unique");
                }
                yield return (label, loc);
            }
        }
    }
}