﻿using AngleSharp;
using AngleSharp.DOM.Css;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class HtmlCssIntegration
    {
        [TestMethod]
        public void DetectStylesheet()
        {
            var html = @"<!DOCTYPE html>

<html>
<head>
    <meta charset=""utf-8"" />
    <title></title>
    <style>
        body
        {
            background-color: green !important;
        }
    </style>
</head>
<body>
</body>
</html>";

            var doc = DocumentBuilder.Html(html);
            Assert.AreEqual(1, doc.StyleSheets.Length);
            var css = doc.StyleSheets[0] as CSSStyleSheet;
            Assert.AreEqual(1, css.CssRules.Length);
            var style = css.CssRules[0] as CSSStyleRule;
            Assert.AreEqual("body", style.SelectorText);
            Assert.AreEqual(1, style.Style.Length);
            var rule = style.Style.List[0];
            Assert.IsTrue(rule.Important);
            Assert.AreEqual("background-color", rule.Name);
            Assert.AreEqual("green", rule.Value.CssText);
        }
    }
}
