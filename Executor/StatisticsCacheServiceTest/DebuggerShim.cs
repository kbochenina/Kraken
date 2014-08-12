using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;

/*
 * Howdy,
 * 
 * This is NSpec's DebuggerShim.  It will allow you to use TestDriven.Net or Resharper's test runner to run
 * NSpec tests that are in the same Assembly as this class.  
 * 
 * It's DEFINITELY worth trying specwatchr (http://nspec.org/continuoustesting). Specwatchr automatically
 * runs tests for you.
 * 
 * If you ever want to debug a test when using Specwatchr, simply put the following line in your test:
 * 
 *     System.Diagnostics.Debugger.Launch()
 *     
 * Visual Studio will detect this and will give you a window which you can use to attach a debugger.
 */

//[TestFixture]
[TestClass]
public class DebuggerShim
{
    //[Test]
    [TestMethod]
    public void debug()
    {
        var tagOrClassName = "class_or_tag_you_want_to_debug";

        var types = GetType().Assembly.GetTypes(); 
        // OR
        types = new System.Type[] { typeof(StatisticsCacheServiceTest.NSpecTests.garbage_collecting_test) };
        var finder = new SpecFinder(types, "");
        var builder = new ContextBuilder(finder, new Tags().Parse(tagOrClassName), new DefaultConventions());
        var runner = new ContextRunner(builder, new ConsoleFormatter(), false);
        var results = runner.Run(builder.Contexts().Build());

        //assert that there aren't any failures
        results.Failures().Count().should_be(0);
    }
}
