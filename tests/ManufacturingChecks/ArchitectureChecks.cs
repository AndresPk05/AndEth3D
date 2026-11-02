using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class ArchitectureChecks
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "backend", "Controllers"))) root = root.Parent;
        if (root is null) throw new Exception("Repository root not found for architecture checks.");
        var files = Directory.GetFiles(Path.Combine(root.FullName, "backend", "Controllers"), "*.cs");
        foreach (var file in files) {
            EnsureThin(File.ReadAllText(file));
            Console.WriteLine("PASS thin HTTP controller: " + Path.GetFileName(file));
        }
        foreach (var file in Directory.GetFiles(Path.Combine(root.FullName, "backend", "Services"), "*.cs", SearchOption.AllDirectories)) {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            if (tree.Usings.Any(u => u.Name!.ToString().Contains("Controllers") || u.Name.ToString().Contains("Microsoft.AspNetCore.Mvc") || u.Name.ToString().Contains("AndEth3D.Backend.Http")))
                throw new Exception("Application/infrastructure service depends on HTTP/controller layer: " + file);
        }
        foreach (var bad in new[] {
            "class BadController(BadService service) { public int Get() { return 3; } }",
            "class BadController(BadService service) { public int Get() => service.Get().Where(x => x.Enabled).ToHttp(this); }",
            "class BadController(AppDbContext db) { public int Get() => db.SaveChanges().ToHttp(this); }"
        }) {
            var rejected = false; try { EnsureThin(bad); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Architecture checker accepted business logic in controller.");
        }
        Console.WriteLine("PASS architecture rejects controller business logic and inverted dependencies");
    }
    private static void EnsureThin(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var controller = root.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax>().SelectMany(n => n.Members)
            .OfType<ClassDeclarationSyntax>().SingleOrDefault() ?? root.Members.OfType<ClassDeclarationSyntax>().Single();
        var parameter = controller.ParameterList?.Parameters;
        if (parameter?.Count != 1 || parameter.Value[0].Identifier.Text != "service" || !parameter.Value[0].Type!.ToString().EndsWith("Service"))
            throw new InvalidOperationException("Controllers must depend only on an application service.");
        if (controller.Members.Any(m => m is not MethodDeclarationSyntax)) throw new InvalidOperationException("Controller contains non-endpoint members.");
        foreach (var method in controller.Members.OfType<MethodDeclarationSyntax>()) {
            if (method.Body is not null || method.ExpressionBody is null) throw new InvalidOperationException("Actions must be expression-bodied gateways.");
            var calls = method.ExpressionBody.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            if (calls.Length != 2 || !calls.Any(c => c.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "ToHttp") ||
                !calls.Any(c => c.Expression is MemberAccessExpressionSyntax m && m.Expression is IdentifierNameSyntax id && id.Identifier.Text == "service"))
                throw new InvalidOperationException("Actions may only delegate and map their result to HTTP.");
            if (method.DescendantNodes().Any(n => n is ObjectCreationExpressionSyntax or LambdaExpressionSyntax or ConditionalExpressionSyntax))
                throw new InvalidOperationException("Business logic belongs in the application layer.");
        }
    }
}
