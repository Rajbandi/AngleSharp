﻿using AngleSharp.DOM.Collections;
using System;

namespace AngleSharp.DOM.Css
{
    /// <summary>
    /// Represents a CSS @keyframe rule.
    /// </summary>
    sealed class CSSKeyframeRule : CSSRule
    {
        #region Members

        string keyText;
        CSSStyleDeclaration style;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new @keyframe rule.
        /// </summary>
        public CSSKeyframeRule()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the key of the keyframe, like '10%', '75%'. The from keyword maps to '0%' and the to keyword maps to '100%'.
        /// </summary>
        public string KeyText
        {
            get { return keyText; }
            set { keyText = value; }
        }

        /// <summary>
        /// Gets a CSSStyleDeclaration of the CSS style associated with the key from.
        /// </summary>
        public CSSStyleDeclaration Style
        {
            get { return style; }
            internal set { style = value; }
        }

        #endregion
    }
}