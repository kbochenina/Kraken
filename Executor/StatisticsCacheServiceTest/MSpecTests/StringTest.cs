using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Machine.Specifications;

namespace StatisticsCacheServiceTest.MSpecTests
{
    [Subject(typeof(StatisticsCacheServiceTest.MSpecTests.StringUtilities))]
    public class when_concatenating_two_strings
    {
        static StringUtilities sut;
        static string input1;
        static string input2;
        static string actualValue;

        Establish that = () =>
        {
            sut = new StringUtilities();
            input1 = "Hello ";
            input2 = "World!";
        };

        Because of = () =>
            actualValue = sut.Concatenate(input1, input2);

        It should_concatenate_both_input_strings = () =>
            actualValue.ShouldEqual("Hello World!");
    }
    public class StringUtilities
    {
        public string Concatenate(string input1, string input2)
        {
            return input1 + input2;
        }
    }
}
