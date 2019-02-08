using System.Threading.Tasks;
using Assent;
using MLS.Protocol;
using MLS.Protocol.Execution;
using Recipes;
using WorkspaceServer.Tests;
using Xunit;
using Xunit.Abstractions;

namespace MLS.Agent.Tests
{
    public class ApiOutputContractTests : ApiViaHttpTestsBase
    {
        private readonly Configuration configuration;

        public ApiOutputContractTests(ITestOutputHelper output) : base(output)
        {
            configuration = new Configuration()
                .UsingExtension("json");

            configuration = configuration.SetInteractive(false);
        }

        [Fact]
        public async Task The_Run_contract_for_compiling_code_has_not_been_broken()
        {
            var viewport = ViewportCode();

            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun");

            var response = await CallRun(requestJson.ToJson());

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        [Fact]
        public async Task The_Run_contract_for_noncompiling_code_has_not_been_broken()
        {
            var viewport = ViewportCode("doesn't compile");

            var request = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun");

            var requestBody = request.ToJson();

            var response = await CallRun(requestBody);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }


        [Fact]
        public async Task The_Compile_contract_for_compiling_code_has_not_been_broken()
        {
            var viewport = ViewportCode();

            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun");

            var response = await CallCompile(requestJson.ToJson());

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        [Fact]
        public async Task The_Compile_contract_for_noncompiling_code_has_not_been_broken()
        {
            var viewport = ViewportCode("doesn't compile");

            var request = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun");

            var requestBody = request.ToJson();

            var response = await CallCompile(requestBody);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }
        [Fact]
        public async Task The_Completions_contract_has_not_been_broken()
        {
            var viewport = ViewportCode("Console.Ou$$");

            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun").ToJson();

            var response = await CallCompletion(requestJson);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        [Fact]
        public async Task The_signature_help_contract_has_not_been_broken()
        {
            var viewport = ViewportCode("Console.Write($$);");

            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode(),
                        viewport
                    }),
                activeBufferId: viewport.Id,
                requestId: "TestRun").ToJson();

            var response = await CallSignatureHelp(requestJson);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        [Fact]
        public async Task The_instrumentation_contract_has_not_been_broken()
        {

            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode("int a = 1; int b = 2; a = 3; b = a;")
                    },
                    includeInstrumentation: true),
                requestId: "TestRun"
            ).ToJson();

            var response = await CallRun(requestJson);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        [Fact]
        public async Task The_run_contract_with_no_instrumentation_has_not_been_broken()
        {
            var requestJson = new WorkspaceRequest(
                new Workspace(
                    workspaceType: "console",
                    buffers: new[]
                    {
                        EntrypointCode("int a = 1; int b = 2; a = 3; b = a;")
                    },
                    includeInstrumentation: false),
                requestId: "TestRun"
            ).ToJson();

            var response = await CallRun(requestJson);

            var result = await response.Content.ReadAsStringAsync();

            this.Assent(result.FormatJson(), configuration);
        }

        private static Workspace.Buffer EntrypointCode(string mainContent = @"Console.WriteLine(Sample.Method());$$")
        {
            var input = $@"
using System;
using System.Linq;

namespace Example
{{
    public class Program
    {{
        public static void Main()
        {{
            {mainContent}
        }}       
    }}
}}".EnforceLF();
                
            MarkupTestFile.GetPosition(input, out string output, out var position);

            return new Workspace.Buffer(
                "Program.cs",
                output,
                position ?? 0);
        }

        private static Workspace.Buffer ViewportCode(string methodContent = @"return ""Hello world!"";$$ ")
        {
            var input = $@"
using System.Collections.Generic;
using System;

namespace Example
{{
    public static class Sample
    {{
        public static object Method()
        {{
#region viewport
            {methodContent}
#endregion
        }}
    }}
}}".EnforceLF();

            MarkupTestFile.GetPosition(input, out string output, out var position);

            return new Workspace.Buffer(
                "ViewportCode.cs",
                output,
                position ?? 0);
        }
    }
}