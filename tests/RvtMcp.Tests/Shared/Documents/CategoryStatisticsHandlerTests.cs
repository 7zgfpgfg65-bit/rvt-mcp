// Fork addition by 7zgfpgfg65-bit, 2026-11-12: execute the production handler at a fake Revit boundary.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RvtMcp.Tests.Shared.Documents;

public class CategoryStatisticsHandlerTests
{
    private static readonly Lazy<MethodInfo> Probe = new(() => Compile());
    [Theory] [InlineData(100, true, 3)] [InlineData(2, false, 2)]
    public void Handler_excludes_types_and_link_contents_includes_annotation_and_marks_cap(int cap, bool complete, int processed)
    {
        var result = JObject.Parse((string)Probe.Value.Invoke(null, new object[] { cap }));
        Assert.Equal(processed, result.Value<int>("elementsCounted"));
        Assert.Equal(complete, result.Value<bool>("categoryIdCountsComplete"));
        Assert.Equal(!complete, result.Value<bool>("truncated"));
        var categories = (JArray)result["categoryIdCounts"];
        Assert.Equal(new long[] { -2, -1 }, categories.Select(x => x.Value<long>("categoryId")));
        Assert.All(categories, x => Assert.Equal(1, x.Value<int>("count")));
        Assert.Equal(complete ? 1 : 0, result.Value<int>("uncategorizedElementsCounted"));
        Assert.Equal("doc-token", result["document"].Value<string>("documentId"));
        Assert.Equal("host.rvt", result["document"].Value<string>("path"));
        Assert.False(result["categoryIdCountsScope"].Value<bool>("linkedDocumentContentsIncluded"));
    }
    private static MethodInfo Compile([CallerFilePath] string file = "")
    {
        var repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", "..", "..", ".."));
        var sources = new[] { "src/shared/Handlers/Documents/AnalyzeModelStatisticsHandler.cs", "src/shared/Documents/CategoryStatistics.cs" }
            .Select(path => File.ReadAllText(Path.Combine(repo, path))).Append(Doubles);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Append(typeof(JObject).Assembly.Location).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CategoryProbe_" + Guid.NewGuid().ToString("N"),
            sources.Select(x => CSharpSyntaxTree.ParseText(x)), references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var emitted = compilation.Emit(output);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        return Assembly.Load(output.ToArray()).GetType("CategoryProbe").GetMethod("Run");
    }
    private const string Doubles = """
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace Autodesk.Revit.DB {
 public class ElementId { public long Value; }
 public class Category { public ElementId Id; public string Name; }
 public class Element { public Category Category; public bool IsType; }
 public class Document { public string Title="Host", PathName="host.rvt"; public List<Element> Elements=new List<Element>(); }
 public class FilteredElementCollector : IEnumerable<Element> {
  private IEnumerable<Element> elements;
  public FilteredElementCollector(Document doc) { elements=doc.Elements; }
  public FilteredElementCollector WhereElementIsNotElementType() { elements=elements.Where(x=>!x.IsType); return this; }
  public IEnumerator<Element> GetEnumerator()=>elements.GetEnumerator();
  System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
 }
}
namespace Autodesk.Revit.UI {
 public class UIDocument { public Autodesk.Revit.DB.Document Document; }
 public class Application { public string VersionNumber="2027"; }
 public class UIApplication { public UIDocument ActiveUIDocument; public Application Application=new Application(); }
}
namespace RvtMcp.Plugin {
 public interface IRevitCommand { }
 public class CommandResult { public object Data; public static CommandResult Ok(object data)=>new CommandResult{Data=data}; public static CommandResult Fail(string error)=>throw new System.Exception(error); }
 public static class RevitCompat { public static long GetId(Autodesk.Revit.DB.ElementId id)=>id.Value; }
 public static class DocumentTargetGuard { public static string GetOrCreateToken(Autodesk.Revit.DB.Document doc)=>"doc-token"; }
}
public static class CategoryProbe {
 public static string Run(int cap) {
  var host=new Autodesk.Revit.DB.Document();
  var linked=new Autodesk.Revit.DB.Document();
  var model=new Autodesk.Revit.DB.Category {Id=new Autodesk.Revit.DB.ElementId {Value=-1},Name="Same"};
  var annotation=new Autodesk.Revit.DB.Category {Id=new Autodesk.Revit.DB.ElementId {Value=-2},Name="Same"};
  host.Elements.Add(new Autodesk.Revit.DB.Element {Category=model});
  host.Elements.Add(new Autodesk.Revit.DB.Element {Category=annotation});
  host.Elements.Add(new Autodesk.Revit.DB.Element {Category=null});
  host.Elements.Add(new Autodesk.Revit.DB.Element {Category=model,IsType=true});
  linked.Elements.Add(new Autodesk.Revit.DB.Element {Category=model});
  var app=new Autodesk.Revit.UI.UIApplication {ActiveUIDocument=new Autodesk.Revit.UI.UIDocument {Document=host}};
  return JsonConvert.SerializeObject(new RvtMcp.Plugin.Handlers.AnalyzeModelStatisticsHandler().Execute(app,"{\"maxElements\":"+cap+"}").Data);
 }
}
""";
}
