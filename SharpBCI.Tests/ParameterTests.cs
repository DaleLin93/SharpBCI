using System;
using System.Diagnostics;
using MarukoLib.Lang;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Tests
{

    [TestClass]
    public class ParameterTests
    {

        public enum NodeType
        {
            Html, Header, Body, Div, Span, Em, 
        }

        [TestMethod]
        public void TestPresentConvert()
        {
            string GetName(NodeType type) => type.ToString().ToLowerInvariant();
            NodeType ParseName(string s) => Enum.TryParse(s, true, out NodeType t) ? t : throw new ArgumentException();
            var typeConverter1 = TypeConverter.Of<NodeType, string>(GetName, ParseName);

            string GetNumStr(NodeType type) => ((int)type).ToString();
            NodeType ParseNumStr(string value) => (NodeType) int.Parse(value);
            var typeConverter2 = TypeConverter.Of<NodeType, string>(GetNumStr, ParseNumStr);

            var p0 = Parameter<NodeType>.OfEnum("Node Type");
            var p1 = Parameter<NodeType>.CreateBuilder("Node Type")
                .SetSelectableValuesForEnum(true)
                .SetTypeConverters(typeConverter1)
                .Build();
            var p2 = Parameter<NodeType>.CreateBuilder("Node Type")
                .SetSelectableValuesForEnum(true)
                .SetTypeConverters(typeConverter2)
                .Build();

            var parameters = new IParameterDescriptor[] {p0, p1, p2};

            foreach (var value in Enum.GetValues(typeof(NodeType)))
            {
                foreach (var p in parameters)
                {
                    var presentString = p.ConvertValueToString(value);
                    var parsedValue = p.ParseValueFromString(presentString);
                    Debug.WriteLine("Parameter Name: {0}, Value: '{1}', Present String: '{2}', Parsed Value: '{3}'", 
                        p.Name, value, presentString, parsedValue);
                    Assert.AreEqual(value, parsedValue);
                }
            }

        }

    }
}
