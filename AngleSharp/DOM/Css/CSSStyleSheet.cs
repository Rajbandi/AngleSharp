﻿using AngleSharp.Css;
using AngleSharp.DOM.Collections;
using System;

namespace AngleSharp.DOM.Css
{
    /// <summary>
    /// Represents a CSS Stylesheet.
    /// </summary>
    public class CSSStyleSheet : StyleSheet
    {
        #region Members

        CSSRuleList cssRules;
        CSSRule ownerRule;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new CSS Stylesheet.
        /// </summary>
        public CSSStyleSheet()
        {
            cssRules = new CSSRuleList();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a CSSRuleList of the CSS rules in the style sheet.
        /// </summary>
        public CSSRuleList CssRules
        {
            get { return cssRules; }
        }

        /// <summary>
        /// Gets the @import rule if the stylesheet was importated otherwise it returns null.
        /// </summary>
        public CSSRule OwnerRule
        {
            get { return ownerRule; }
            internal set { ownerRule = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Removes a style rule from the current style sheet object.
        /// </summary>
        /// <param name="index">The index representing the position to be removed.</param>
        /// <returns>The current stylesheet.</returns>
        public CSSStyleSheet DeleteRule(int index)
        {
            if (index >= 0 && index < cssRules.Length)
                cssRules.RemoveAt(index);

            return this;
        }

        /// <summary>
        /// Inserts a new style rule into the current style sheet.
        /// </summary>
        /// <param name="rule">A string containing the rule to be inserted (selector and declaration).</param>
        /// <param name="index">The index representing the position to be inserted.</param>
        /// <returns>The current stylesheet.</returns>
        public CSSStyleSheet InsertRule(string rule, int index)
        {
            if (index >= 0 && index <= cssRules.Length)
            {
                var value = CssParser.ParseRule(rule);
                cssRules.InsertAt(index, value);
            }

            return this;
        }

        #endregion
    }
}