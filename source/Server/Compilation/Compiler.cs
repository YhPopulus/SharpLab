using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FSharp.Compiler;
using FSharp.Compiler.SourceCodeServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using MirrorSharp.Advanced;
using MirrorSharp.FSharp.Advanced;

namespace SharpLab.Server.Compilation {
    public class Compiler : ICompiler {
        private static readonly EmitOptions RoslynEmitOptions = new EmitOptions(
            // TODO: try out embedded
            debugInformationFormat: DebugInformationFormat.PortablePdb
        );

        public async Task<(bool assembly, bool symbols)> TryCompileToStreamAsync(MemoryStream assemblyStream, MemoryStream? symbolStream, IWorkSession session, IList<Diagnostic> diagnostics, CancellationToken cancellationToken) {
            if (session.IsFSharp()) {
                var compiled = await TryCompileFSharpToStreamAsync(assemblyStream, session, diagnostics, cancellationToken).ConfigureAwait(false);
                return (compiled, false);
            }

            var compilation = await session.Roslyn.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var emitResult = compilation.Emit(assemblyStream, pdbStream: symbolStream, options: RoslynEmitOptions);
            if (!emitResult.Success) {
                foreach (var diagnostic in emitResult.Diagnostics) {
                    diagnostics.Add(diagnostic);
                }
                return (false, false);
            }
            return (true, true);
        }

        private async Task<bool> TryCompileFSharpToStreamAsync(MemoryStream assemblyStream, IWorkSession session, IList<Diagnostic> diagnostics, CancellationToken cancellationToken) {
            var fsharp = session.FSharp();

            // GetLastParseResults are guaranteed to be available here as MirrorSharp's SlowUpdate does the parse
            var parsed = fsharp.GetLastParseResults();
            using (var virtualAssemblyFile = FSharpFileSystem.RegisterVirtualFile(assemblyStream)) {
                var compiled = await FSharpAsync.StartAsTask(fsharp.Checker.Compile(
                    // ReSharper disable once PossibleNullReferenceException
                    FSharpList<Ast.ParsedInput>.Cons(parsed.ParseTree.Value, FSharpList<Ast.ParsedInput>.Empty),
                    "_", virtualAssemblyFile.Name,
                    fsharp.AssemblyReferencePathsAsFSharpList,
                    pdbFile: null,
                    executable: false,//fsharp.ProjectOptions.OtherOptions.Contains("--target:exe"),
                    noframework: true,
                    userOpName: null
                ), null, cancellationToken).ConfigureAwait(false);
                foreach (var error in compiled.Item1) {
                    // no reason to add warnings as check would have added them anyways
                    if (error.Severity.Tag == FSharpErrorSeverity.Tags.Error)
                        diagnostics.Add(fsharp.ConvertToDiagnostic(error));
                }
                return virtualAssemblyFile.Stream.Length > 0;
            }
        }
    }
}