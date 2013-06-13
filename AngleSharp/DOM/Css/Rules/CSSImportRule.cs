﻿using AngleSharp.DOM.Collections;
using System;

namespace AngleSharp.DOM.Css
{
    /// <summary>
    /// Represents a CSS import rule.
    /// </summary>
    sealed class CSSImportRule : CSSRule
    {
        #region Members

        string _href;
        MediaList _media;
        CSSStyleSheet _styleSheet;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new CSS import rule
        /// </summary>
        public CSSImportRule()
        {
            _media = new MediaList();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the location of the style sheet to be imported. 
        /// </summary>
        public string Href
        {
            get { return _href; }
            internal set { _href = value; }
        }

        /// <summary>
        /// Gets a list of media types for which this style sheet may be used.
        /// </summary>
        public MediaList Media
        {
            get { return _media; }
        }

        /// <summary>
        /// Gets the style sheet referred to by this rule, if it has been loaded. 
        /// </summary>
        public CSSStyleSheet StyleSheet
        {
            get { return _styleSheet; }
            internal set { _styleSheet = value; }
        }

        #endregion
    }
}