﻿using AngleSharp.DOM.Collections;
using System;

namespace AngleSharp.DOM.Css
{
    /// <summary>
    /// Represents the @font-face rule.
    /// </summary>
    class CSSFontFaceRule : CSSRule
    {
        #region Members

        CSSStyleDeclaration style;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new @font-face rule.
        /// </summary>
        public CSSFontFaceRule()
        {
            style = new CSSStyleDeclaration();
        }

        #endregion

        #region Internal methods

        /// <summary>
        /// Appends the given rule to the list of rules.
        /// </summary>
        /// <param name="rule">The rule to append.</param>
        /// <returns>The current font-face rule.</returns>
        internal CSSFontFaceRule AppendRule(CSSProperty rule)
        {
            style.AppendProperty(rule);
            return this;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the font-family.
        /// </summary>
        public string Family
        {
            get { return style.GetPropertyValue("font-family"); }
            set { style.SetProperty("font-family", value); }
        }

        /// <summary>
        /// Gets or sets the source of the font.
        /// </summary>
        public string Src
        {
            get { return style.GetPropertyValue("src"); }
            set { style.SetProperty("src", value); }
        }

        /// <summary>
        /// Gets or sets the style of the font.
        /// </summary>
        public string Style
        {
            get { return style.GetPropertyValue("font-style"); }
            set { style.SetProperty("font-style", value); }
        }

        /// <summary>
        /// Gets or sets the weight of the font.
        /// </summary>
        public string Weight
        {
            get { return style.GetPropertyValue("font-weight"); }
            set { style.SetProperty("font-weight", value); }
        }

        /// <summary>
        /// Gets or sets the stretch value of the font.
        /// </summary>
        public string Stretch
        {
            get { return style.GetPropertyValue("stretch"); }
            set { style.SetProperty("stretch", value); }
        }

        /// <summary>
        /// Gets or sets the unicode range of the font.
        /// </summary>
        public string UnicodeRange
        {
            get { return style.GetPropertyValue("unicode-range"); }
            set { style.SetProperty("unicode-range", value); }
        }

        /// <summary>
        /// Gets or sets the variant of the font.
        /// </summary>
        public string Variant
        {
            get { return style.GetPropertyValue("font-variant"); }
            set { style.SetProperty("font-variant", value); }
        }

        /// <summary>
        /// Gets or sets the feature settings of the font.
        /// </summary>
        public string FeatureSettings
        {
            get { return style.GetPropertyValue("font-feature-settings"); }
            set { style.SetProperty("font-feature-settings", value); }
        }

        #endregion
    }
}