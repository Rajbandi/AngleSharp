﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AngleSharp.DOM;
using AngleSharp.DOM.Collections;
using AngleSharp.DOM.Html;
using AngleSharp.DOM.Mathml;
using AngleSharp.DOM.Svg;
using AngleSharp.DOM.Xml;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AngleSharp.Html
{
    /// <summary>
    /// Represents the Tree construction as specified in
    /// 8.2.5 Tree construction, on the following page:
    /// http://www.w3.org/html/wg/drafts/html/master/syntax.html
    /// </summary>
    [DebuggerStepThrough]
    public class HtmlParser : IParser
    {
        #region Members

        HtmlTokenizer tokenizer;
        HTMLDocument doc;
        HtmlTreeMode insert;
        HtmlTreeMode originalInsert;
        List<Element> open;
        List<Element> formatting;
        HTMLFormElement form;
        Boolean frameset;
        Node fragmentContext;
        Boolean foster;
        Int32 nesting;
        Boolean pause;
        Boolean started;
        TaskCompletionSource<Boolean> tcs;
        HTMLScriptElement pendingParsingBlock;

        #endregion

        #region Events

        /// <summary>
        /// The event will be fired once an error has been detected.
        /// </summary>
        public event EventHandler<ParseErrorEventArgs> ErrorOccurred;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new instance of the HTML parser with an new document
        /// based on the given source.
        /// </summary>
        /// <param name="source">The source code as a string.</param>
        public HtmlParser(String source)
            : this(new HTMLDocument(), new SourceManager(source))
        {
        }

        /// <summary>
        /// Creates a new instance of the HTML parser with an new document
        /// based on the given stream.
        /// </summary>
        /// <param name="stream">The stream to use as source.</param>
        public HtmlParser(Stream stream)
            : this(new HTMLDocument(), new SourceManager(stream))
        {
        }

        /// <summary>
        /// Creates a new instance of the HTML parser with the specified document
        /// based on the given source.
        /// </summary>
        /// <param name="document">The document instance to be constructed.</param>
        /// <param name="source">The source code as a string.</param>
        public HtmlParser(HTMLDocument document, String source)
            : this(document, new SourceManager(source))
        {
        }

        /// <summary>
        /// Creates a new instance of the HTML parser with the specified document
        /// based on the given stream.
        /// </summary>
        /// <param name="document">The document instance to be constructed.</param>
        /// <param name="stream">The stream to use as source.</param>
        public HtmlParser(HTMLDocument document, Stream stream)
            : this(document, new SourceManager(stream))
        {
        }

        /// <summary>
        /// Creates a new instance of the HTML parser with the specified document
        /// based on the given source manager.
        /// </summary>
        /// <param name="document">The document instance to be constructed.</param>
        /// <param name="source">The source to use.</param>
        internal HtmlParser(HTMLDocument document, SourceManager source)
        {
            tokenizer = new HtmlTokenizer(source);

            tokenizer.ErrorOccurred += (s, ev) =>
            {
                if (ErrorOccurred != null)
                    ErrorOccurred(this, ev);
            };

            started = false;
            doc = document;
            open = new List<Element>();
            formatting = new List<Element>();
            frameset = true;
            insert = HtmlTreeMode.Initial;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the (maybe intermediate) result of the parsing process.
        /// </summary>
        public HTMLDocument Result
        {
            get 
            {
                Parse();
                return doc; 
            }
        }

        /// <summary>
        /// Gets if the parser has been started asynchronously.
        /// </summary>
        public Boolean IsAsync
        {
            get { return tcs != null; }
        }

        /// <summary>
        /// Gets if the tree builder has been created for
        /// parsing HTML fragments.
        /// </summary>
        public Boolean IsFragmentCase
        {
            get { return fragmentContext != null; }
        }

        /// <summary>
        /// Gets the adjusted current node.
        /// </summary>
        internal Node AdjustedCurrentNode
        {
            get { return (fragmentContext != null && open.Count == 1) ? fragmentContext : CurrentNode; }
        }

        /// <summary>
        /// Gets the current node.
        /// </summary>
        internal Element CurrentNode
        {
            get { return open.Count > 0 ? open[open.Count - 1] : null; }
        }

        /// <summary>
        /// Gets or sets the pending parsing block script.
        /// </summary>
        internal HTMLScriptElement PendingParsingBlock
        {
            get { return pendingParsingBlock; }
            set { pendingParsingBlock = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Parses the given source asynchronously and creates the document.
        /// WARNING: This method is not yet implemented.
        /// </summary>
        /// <returns>The task which could be awaited or continued differently.</returns>
        public Task ParseAsync()
        {
            if (!started)
            {
                started = true;
                tcs = new TaskCompletionSource<bool>();
                //TODO
                return tcs.Task;
            }
            else if (tcs == null)
            {
                var temp = new TaskCompletionSource<bool>();
                temp.SetResult(true);
                return temp.Task;
            }

            return tcs.Task;
        }

        /// <summary>
        /// Parses the given source and creates the document.
        /// </summary>
        public void Parse()
        {
            if (!started)
            {
                started = true;
                HtmlToken token;

                do
                {
                    token = tokenizer.Get();
                    Consume(token);
                }
                while (token.Type != HtmlTokenType.EOF);
            }
        }

        /// <summary>
        /// Switches to the fragment algorithm with the specified context element.
        /// </summary>
        /// <param name="context">The context element where the algorithm is applied to.</param>
        public void SwitchToFragment(Node context)
        {
            if (started)
                throw new InvalidOperationException("Fragment mode has to be activated before running the parser!");

            switch (context.NodeName)
            {
                case HTMLTitleElement.Tag:
                case HTMLTextAreaElement.Tag:
                {
                    tokenizer.Switch(HtmlParseMode.RCData);
                    break;
                }
                case HTMLStyleElement.Tag:
                case HTMLSemanticElement.XmpTag:
                case HTMLIFrameElement.Tag:
                case HTMLNoElement.NoEmbedTag:
                case HTMLNoElement.NoFramesTag:
                {
                    tokenizer.Switch(HtmlParseMode.Rawtext);
                    break;
                }
                case HTMLScriptElement.Tag:
                {
                    tokenizer.Switch(HtmlParseMode.Script);
                    break;
                }
                case HTMLNoElement.NoScriptTag:
                {
                    if (doc.IsScripting) 
                        tokenizer.Switch(HtmlParseMode.Rawtext);

                    break;
                }
                case HTMLSemanticElement.PlaintextTag:
                {
                    tokenizer.Switch(HtmlParseMode.Plaintext);
                    break;
                }
            }

            var root = new HTMLHtmlElement();
            doc.AppendChild(root);
            open.Add(root);
            Reset(context);

            fragmentContext = context;
            tokenizer.AcceptsCharacterData = !AdjustedCurrentNode.IsInHtml;

            do
            {
                if (context is HTMLFormElement)
                {
                    form = (HTMLFormElement)context;
                    break;
                }

                context = context.ParentNode;
            }
            while (context != null);
        }

        /// <summary>
        /// Resets the current insertation mode to the
        /// rules according to the algorithm specified
        /// in 8.2.3.1 The insertion mode.
        /// </summary>
        void Reset(Node context = null)
        {
            var last = false;
            Node node;

            for (var i = open.Count - 1; i >= 0; i--)
            {
                node = open[i];

                if (i == 0)
                {
                    last = true;
                    node = context ?? node;
                }

                switch (node.NodeName)
                {
                    case HTMLSelectElement.Tag:
                        insert = HtmlTreeMode.InSelect;
                        break;
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableCellElement.NormalTag:
                        insert = last ? HtmlTreeMode.InBody : HtmlTreeMode.InCell;
                        break;
                    case HTMLTableRowElement.Tag:
                        insert = HtmlTreeMode.InRow;
                        break;
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableSectionElement.FootTag:
                    case HTMLTableSectionElement.BodyTag:
                        insert = HtmlTreeMode.InTableBody;
                        break;
                    case HTMLTableCaptionElement.Tag:
                        insert = HtmlTreeMode.InCaption;
                        break;
                    case HTMLTableColElement.ColgroupTag:
                        insert = HtmlTreeMode.InColumnGroup;
                        break;
                    case HTMLTableElement.Tag:
                        insert = HtmlTreeMode.InTable;
                        break;
                    case HTMLHeadElement.Tag:
                        insert = HtmlTreeMode.InBody;
                        break;
                    case HTMLBodyElement.Tag:
                        insert = HtmlTreeMode.InBody;
                        break;
                    case HTMLFrameSetElement.Tag:
                        insert = HtmlTreeMode.InFrameset;
                        break;
                    case HTMLHtmlElement.Tag:
                        insert = HtmlTreeMode.BeforeHead;
                        break;
                    default:
                        if (last)
                        {
                            insert = HtmlTreeMode.InBody;
                            break;
                        }

                        continue;
                }

                break;
            }
        }

        /// <summary>
        /// Consumes a token and processes it.
        /// </summary>
        /// <param name="token">The token to consume.</param>
        void Consume(HtmlToken token)
        {
            var node = AdjustedCurrentNode;

            if (node == null || node.IsInHtml || token.IsEof || (node.IsHtmlTIP && token.IsHtmlCompatible) || 
                (node.IsMathMLTIP && token.IsMathCompatible) || (node.IsInMathMLSVGReady && token.IsSvg))
            {
                switch (insert)
                {
                    case HtmlTreeMode.Initial:
                        Initial(token);
                        break;

                    case HtmlTreeMode.BeforeHtml:
                        BeforeHtml(token);
                        break;

                    case HtmlTreeMode.BeforeHead:
                        BeforeHead(token);
                        break;

                    case HtmlTreeMode.InHead:
                        InHead(token);
                        break;

                    case HtmlTreeMode.InHeadNoScript:
                        InHeadNoScript(token);
                        break;

                    case HtmlTreeMode.AfterHead:
                        AfterHead(token);
                        break;

                    case HtmlTreeMode.InBody:
                        InBody(token);
                        break;

                    case HtmlTreeMode.Text:
                        Text(token);
                        break;

                    case HtmlTreeMode.InTable:
                        InTable(token);
                        break;

                    case HtmlTreeMode.InCaption:
                        InCaption(token);
                        break;

                    case HtmlTreeMode.InColumnGroup:
                        InColumnGroup(token);
                        break;

                    case HtmlTreeMode.InTableBody:
                        InTableBody(token);
                        break;

                    case HtmlTreeMode.InRow:
                        InRow(token);
                        break;

                    case HtmlTreeMode.InCell:
                        InCell(token);
                        break;

                    case HtmlTreeMode.InSelect:
                        InSelect(token);
                        break;

                    case HtmlTreeMode.InSelectInTable:
                        InSelectInTable(token);
                        break;

                    case HtmlTreeMode.AfterBody:
                        AfterBody(token);
                        break;

                    case HtmlTreeMode.InFrameset:
                        InFrameset(token);
                        break;

                    case HtmlTreeMode.AfterFrameset:
                        AfterFrameset(token);
                        break;

                    case HtmlTreeMode.AfterAfterBody:
                        AfterAfterBody(token);
                        break;

                    case HtmlTreeMode.AfterAfterFrameset:
                        AfterAfterFrameset(token);
                        break;
                }
            }
            else
                Foreign(token);
        }

        #endregion

        #region Home

        /// <summary>
        /// See 8.2.5.4.1 The "initial" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void Initial(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(doc, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                var doctype = (HtmlDoctypeToken)token;

                if (!doctype.IsValid)
                    RaiseErrorOccurred(ErrorCode.DoctypeInvalid);

                AddDoctype(doctype);

                if (doctype.IsFullQuirks)
                    doc.QuirksMode = QuirksMode.On;
                else if (doctype.IsLimitedQuirks)
                    doc.QuirksMode = QuirksMode.Limited;

                insert = HtmlTreeMode.BeforeHtml;
                return;
            }
            else if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                chars.TrimStart();

                if (chars.IsEmpty)
                    return;
            }

            if (!doc.IsEmbedded)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeMissing);
                doc.QuirksMode = QuirksMode.On;
            }

            insert = HtmlTreeMode.BeforeHtml;
            BeforeHtml(token);
        }

        /// <summary>
        /// See 8.2.5.4.2 The "before html" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void BeforeHtml(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(doc, token);
                return;
            }
            else if (token.Type == HtmlTokenType.StartTag && ((HtmlTagToken)token).Name == HTMLHtmlElement.Tag)
            {
                AddRoot(token);

                //TODO
                //If the Document is being loaded as part of navigation of a browsing context, then:
                //  if the newly created element has a manifest attribute whose value is not the empty string,
                //    then resolve the value of that attribute to an absolute URL, relative to the newly created element,
                //    and if that is successful, run the application cache selection algorithm with the result of applying
                //    the URL serializer algorithm to the resulting parsed URL with the exclude fragment flag set;
                //  otherwise, if there is no such attribute, or its value is the empty string, or resolving its value fails,
                //    run the application cache selection algorithm with no manifest. The algorithm must be passed the Document object.
                insert = HtmlTreeMode.BeforeHead;
                return;
            }
            else if(token.IsEndTagInv(HTMLHtmlElement.Tag, HTMLBodyElement.Tag, HTMLBRElement.Tag, HTMLHeadElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                return;
            }
            else if(token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                chars.TrimStart();

                if (chars.IsEmpty)
                    return;
            }

            BeforeHtml(HtmlToken.OpenTag(HTMLHtmlElement.Tag));
            //TODO
            //If the Document is being loaded as part of navigation of a browsing context, then:
            //  run the application cache selection algorithm with no manifest, passing it the Document object.
            BeforeHead(token);
        }

        /// <summary>
        /// See 8.2.5.4.3 The "before head" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void BeforeHead(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.Type == HtmlTokenType.StartTag && ((HtmlTagToken)token).Name == HTMLHtmlElement.Tag)
            {
                InBody(token);
                return;
            }
            else if (token.Type == HtmlTokenType.StartTag && ((HtmlTagToken)token).Name == HTMLHeadElement.Tag)
            {
                var element = new HTMLHeadElement();
                AddElementToCurrentNode(element, token);
                insert = HtmlTreeMode.InHead;
                return;
            }
            else if (token.IsEndTagInv(HTMLHtmlElement.Tag, HTMLBodyElement.Tag, HTMLBRElement.Tag, HTMLHeadElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                return;
            }
            else if(token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                chars.TrimStart();

                if (chars.IsEmpty)
                    return;
            }

            BeforeHead(HtmlToken.OpenTag(HTMLHeadElement.Tag));
            InHead(token);
        }
        
        /// <summary>
        /// See 8.2.5.4.4 The "in head" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InHead(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                var str = chars.TrimStart();
                InsertCharacters(str);

                if (chars.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.IsStartTag(HTMLHtmlElement.Tag))
            {
                InBody(token);
                return;
            }
            else if (token.IsStartTag(HTMLBaseElement.Tag, HTMLBaseFontElement.Tag, HTMLBgsoundElement.Tag, HTMLLinkElement.Tag))
            {
                var name = ((HtmlTagToken)token).Name;
                var element = HTMLElement.Factory(name);
                AddElementToCurrentNode(element, token, true);
                CloseCurrentNode();
                return;
            }
            else if (token.IsStartTag(HTMLMetaElement.Tag))
            {
                var element = new HTMLMetaElement();
                AddElementToCurrentNode(element, token, true);
                CloseCurrentNode();

                var charset = element.GetAttribute(HtmlEncoding.CHARSET);

                if (charset != null && HtmlEncoding.IsSupported(charset))
                {
                    SetCharset(charset);
                    return;
                }

                charset = element.GetAttribute("http-equiv");

                if (charset != null && charset.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    charset = element.GetAttribute("content") ?? string.Empty;
                    charset = HtmlEncoding.Extract(charset);

                    if (HtmlEncoding.IsSupported(charset))
                        SetCharset(charset);
                }

                return;
            }
            else if (token.IsStartTag(HTMLTitleElement.Tag))
            {
                RCDataAlgorithm((HtmlTagToken)token);
                return;
            }
            else if (token.IsStartTag(HTMLNoElement.NoFramesTag, HTMLStyleElement.Tag) || (doc.IsScripting && token.IsStartTag(HTMLNoElement.NoScriptTag)))
            {
                RawtextAlgorithm((HtmlTagToken)token);
                return;
            }
            else if (token.IsStartTag(HTMLNoElement.NoScriptTag))
            {
                var element = new HTMLElement();
                AddElementToCurrentNode(element, token);
                insert = HtmlTreeMode.InHeadNoScript;
                return;
            }
            else if (token.IsStartTag(HTMLScriptElement.Tag))
            {
                var element = new HTMLScriptElement();
                //element.IsParserInserted = true;
                //element.IsAlreadyStarted = fragment;
                AddElementToCurrentNode(element, token);
                tokenizer.Switch(HtmlParseMode.Script);
                originalInsert = insert;
                insert = HtmlTreeMode.Text;
                return;
            }
            else if (token.IsEndTag(HTMLHeadElement.Tag))
            {
                CloseCurrentNode();
                insert = HtmlTreeMode.AfterHead;
                return;
            }
            else if (token.IsStartTag(HTMLHeadElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.HeadTagMisplaced);
                return;
            }
            else if (token.IsEndTagInv(HTMLHtmlElement.Tag, HTMLBodyElement.Tag, HTMLBRElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                return;
            }

            CloseCurrentNode();
            insert = HtmlTreeMode.AfterHead;
            AfterHead(token);
        }

        /// <summary>
        /// See 8.2.5.4.5 The "in head noscript" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InHeadNoScript(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.IsStartTag(HTMLHtmlElement.Tag))
            {
                InBody(token);
                return;
            }
            else if (token.IsEndTag(HTMLNoElement.NoScriptTag))
            {
                CloseCurrentNode();
                insert = HtmlTreeMode.InHead;
                return;
            }
            else if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                var str = chars.TrimStart();
                InsertCharacters(str);

                if (chars.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                InHead(token);
                return;
            }
            else if (token.IsStartTag(HTMLBaseFontElement.Tag, HTMLBgsoundElement.Tag, HTMLLinkElement.Tag, HTMLMetaElement.Tag, HTMLNoElement.NoFramesTag, HTMLStyleElement.Tag))
            {
                InHead(token);
                return;
            }
            else if (token.IsStartTag(HTMLHeadElement.Tag, HTMLNoElement.NoScriptTag))
            {
                RaiseErrorOccurred(ErrorCode.TagInappropriate);
                return;
            }
            else if (token.IsEndTagInv(HTMLBRElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
            CloseCurrentNode();
            insert = HtmlTreeMode.InHead;
            InHead(token);
        }

        /// <summary>
        /// See 8.2.5.4.6 The "after head" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void AfterHead(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                var str = chars.TrimStart();
                InsertCharacters(str);

                if (chars.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.IsStartTag(HTMLHtmlElement.Tag))
            {
                InBody(token);
                return;
            }
            else if (token.IsStartTag(HTMLBodyElement.Tag))
            {
                AfterHeadStartTagBody((HtmlTagToken)token);
                return;
            }
            else if (token.IsStartTag(HTMLFrameSetElement.Tag))
            {
                var element = new HTMLFrameSetElement();
                AddElementToCurrentNode(element, token);
                insert = HtmlTreeMode.InFrameset;
                return;
            }
            else if (token.IsStartTag(HTMLBaseElement.Tag, HTMLBaseFontElement.Tag, HTMLBgsoundElement.Tag, HTMLLinkElement.Tag, HTMLMetaElement.Tag, HTMLNoElement.NoFramesTag, HTMLScriptElement.Tag, HTMLStyleElement.Tag, HTMLTitleElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagMustBeInHead);
                var index = open.Count;
                open.Add(doc.Head);
                InHead(token);
                open.RemoveAt(index);
                return;
            }
            else if (token.IsStartTag(HTMLHeadElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.HeadTagMisplaced);
                return;
            }
            else if (token.IsEndTagInv(HTMLHtmlElement.Tag, HTMLBodyElement.Tag, HTMLBRElement.Tag))
            {
                RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                return;
            }

            AfterHeadStartTagBody(HtmlToken.OpenTag(HTMLBodyElement.Tag));
            frameset = true;
            Consume(token);
        }

        /// <summary>
        /// See 8.2.5.4.7 The "in body" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InBody(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                ReconstructFormatting();
                InsertCharacters(chrs.Data);

                if(chrs.HasContent)
                    frameset = false;
            }
            else if (token.Type == HtmlTokenType.Comment)
                AddComment(CurrentNode, token);
            else if (token.Type == HtmlTokenType.DOCTYPE)
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLHtmlElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.HtmlTagMisplaced);
                        AppendAttributes(tag, open[0]);
                        break;
                    }
                    case HTMLBaseElement.Tag:
                    case HTMLBaseFontElement.Tag:
                    case HTMLBgsoundElement.Tag:
                    case HTMLLinkElement.Tag:
                    case HTMLMenuItemElement.Tag:
                    case HTMLMetaElement.Tag:
                    case HTMLNoElement.NoFramesTag:
                    case HTMLScriptElement.Tag:
                    case HTMLStyleElement.Tag:
                    case HTMLTitleElement.Tag:
                    {
                        InHead(token);
                        break;
                    }
                    case HTMLBodyElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.BodyTagMisplaced);

                        if (open.Count > 1 && open[1] is HTMLBodyElement)
                        {
                            frameset = false;
                            AppendAttributes(tag, open[1]);
                        }

                        break;
                    }
                    case HTMLFrameSetElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.FramesetMisplaced);

                        if (open.Count != 1 && open[1] is HTMLBodyElement && frameset)
                        {
                            open[1].ParentNode.RemoveChild(open[1]);

                            while (open.Count > 1)
                                CloseCurrentNode();

                            var element = new HTMLFrameSetElement();
                            AddElementToCurrentNode(element, token);
                            insert = HtmlTreeMode.InFrameset;
                        }

                        break;
                    }
                    case HTMLSemanticElement.AddressTag:
                    case HTMLSemanticElement.ArticleTag:
                    case HTMLSemanticElement.AsideTag:
                    case HTMLQuoteElement.BlockTag:
                    case HTMLSemanticElement.CenterTag:
                    case HTMLDetailsElement.Tag:
                    case HTMLDialogElement.Tag:
                    case HTMLDirectoryElement.Tag:
                    case HTMLDivElement.Tag:
                    case HTMLDListElement.Tag:
                    case HTMLFieldSetElement.Tag:
                    case HTMLSemanticElement.FigcaptionTag:
                    case HTMLSemanticElement.FigureTag:
                    case HTMLSemanticElement.FooterTag:
                    case HTMLSemanticElement.HeaderTag:
                    case HTMLSemanticElement.HgroupTag:
                    case HTMLMenuElement.Tag:
                    case HTMLSemanticElement.NavTag:
                    case HTMLOListElement.Tag:
                    case HTMLParagraphElement.Tag:
                    case HTMLSemanticElement.SectionTag:
                    case HTMLSemanticElement.SummaryTag:
                    case HTMLUListElement.Tag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case HTMLHeadingElement.ChapterTag:
                    case HTMLHeadingElement.SubSubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSectionTag:
                    case HTMLHeadingElement.SubSectionTag:
                    case HTMLHeadingElement.SectionTag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        if (CurrentNode is HTMLHeadingElement)
                        {
                            RaiseErrorOccurred(ErrorCode.HeadingNested);
                            CloseCurrentNode();
                        }

                        var element = new HTMLHeadingElement();
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case HTMLPreElement.Tag:
                    case HTMLSemanticElement.ListingTag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        var element = new HTMLPreElement();
                        AddElementToCurrentNode(element, token);
                        frameset = false;
                        PreventNewLine();
                        break;
                    }
                    case HTMLFormElement.Tag:
                    {
                        if (form == null)
                        {
                            if (IsInButtonScope(HTMLParagraphElement.Tag))
                                InBodyEndTagParagraph();

                            var element = new HTMLFormElement();
                            AddElementToCurrentNode(element, token);
                            form = element;
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.FormAlreadyOpen);

                        break;
                    }
                    case HTMLLIElement.ItemTag:
                    {
                        InBodyStartTagListItem(tag);
                        break;
                    }
                    case HTMLLIElement.DefinitionTag:
                    case HTMLLIElement.DescriptionTag:
                    {
                        InBodyStartTagDefinitionItem(tag);
                        break;
                    }
                    case HTMLSemanticElement.PlaintextTag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        var plaintext = new HTMLElement();
                        AddElementToCurrentNode(plaintext, token);
                        tokenizer.Switch(HtmlParseMode.Plaintext);
                        break;
                    }
                    case HTMLButtonElement.Tag:
                    {
                        if (IsInScope(tag.Name))
                        {
                            RaiseErrorOccurred(ErrorCode.ButtonInScope);
                            InBodyEndTagBlock(tag.Name);
                            InBody(token);
                        }
                        else
                        {
                            ReconstructFormatting();
                            var element = new HTMLButtonElement();
                            AddElementToCurrentNode(element, token);
                            frameset = false;
                        }
                        break;
                    }
                    case HTMLAnchorElement.Tag:
                    {
                        for (var i = formatting.Count - 1; i >= 0; i--)
                        {
                            if (formatting[i] is ScopeMarkerNode)
                                break;
                            else if (formatting[i].NodeName == HTMLAnchorElement.Tag)
                            {
                                var format = formatting[i];
                                RaiseErrorOccurred(ErrorCode.AnchorNested);
                                HeisenbergAlgorithm(HtmlToken.CloseTag(HTMLAnchorElement.Tag));
                                if(open.Contains(format)) open.Remove(format);
                                if(formatting.Contains(format)) formatting.RemoveAt(i);
                                break;
                            }
                        }

                        ReconstructFormatting();
                        var element = new HTMLAnchorElement();
                        AddElementToCurrentNode(element, token);
                        AddFormattingElement(element);
                        break;
                    }
                    case HTMLFormattingElement.BTag:
                    case HTMLFormattingElement.BigTag:
                    case HTMLFormattingElement.CodeTag:
                    case HTMLFormattingElement.EmTag:
                    case HTMLFontElement.Tag:
                    case HTMLFormattingElement.ITag:
                    case HTMLFormattingElement.STag:
                    case HTMLFormattingElement.SmallTag:
                    case HTMLFormattingElement.StrikeTag:
                    case HTMLFormattingElement.StrongTag:
                    case HTMLFormattingElement.TtTag:
                    case HTMLFormattingElement.UTag:
                    {
                        ReconstructFormatting();
                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token);
                        AddFormattingElement(element);
                        break;
                    }
                    case HTMLFormattingElement.NobrTag:
                    {
                        ReconstructFormatting();

                        if (IsInScope(HTMLFormattingElement.NobrTag))
                        {
                            RaiseErrorOccurred(ErrorCode.NobrInScope);
                            HeisenbergAlgorithm(tag);
                            ReconstructFormatting();
                        }

                        var element = new HTMLElement();
                        AddElementToCurrentNode(element, token);
                        AddFormattingElement(element);
                        break;
                    }
                    case HTMLAppletElement.Tag:
                    case HTMLMarqueeElement.Tag:
                    case HTMLObjectElement.Tag:
                    {
                        ReconstructFormatting();
                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token);
                        InsertScopeMarker();
                        frameset = false;
                        break;
                    }
                    case HTMLTableElement.Tag:
                    {
                        if (doc.QuirksMode == QuirksMode.Off && IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        var element = new HTMLTableElement();
                        AddElementToCurrentNode(element, token);
                        frameset = false;
                        insert = HtmlTreeMode.InTable;
                        break;
                    }
                    case HTMLAreaElement.Tag:
                    case HTMLBRElement.Tag:
                    case HTMLEmbedElement.Tag:
                    case HTMLImageElement.Tag:
                    case HTMLKeygenElement.Tag:
                    case HTMLWbrElement.Tag:
                    {
                        InBodyStartTagBreakrow(tag);
                        break;
                    }
                    case HTMLInputElement.Tag:
                    {
                        ReconstructFormatting();
                        var element = new HTMLInputElement();
                        AddElementToCurrentNode(element, token, true);
                        CloseCurrentNode();

                        if (!tag.GetAttribute("type").Equals("hidden", StringComparison.OrdinalIgnoreCase))
                            frameset = false;
                        break;
                    }
                    case HTMLParamElement.Tag:
                    case HTMLSourceElement.Tag:
                    case HTMLTrackElement.Tag:
                    {
                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token, true);
                        CloseCurrentNode();
                        break;
                    }
                    case HTMLHRElement.Tag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        var element = new HTMLHRElement();
                        AddElementToCurrentNode(element, token, true);
                        CloseCurrentNode();
                        frameset = false;
                        break;
                    }
                    case HTMLImageElement.FalseTag:
                    {
                        RaiseErrorOccurred(ErrorCode.ImageTagNamedWrong);
                        tag.Name = HTMLImageElement.Tag;
                        goto case HTMLImageElement.Tag;
                    }
                    case HTMLIsIndexElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TagInappropriate);

                        if (form == null)
                        {
                            InBody(HtmlToken.OpenTag(HTMLFormElement.Tag));

                            if (tag.GetAttribute("action") != String.Empty)
                                form.SetAttribute("action", tag.GetAttribute("action"));

                            InBody(HtmlToken.OpenTag(HTMLHRElement.Tag));
                            InBody(HtmlToken.OpenTag(HTMLLabelElement.Tag));

                            if (tag.GetAttribute("prompt") != String.Empty)
                                InsertCharacters(tag.GetAttribute("prompt"));
                            else
                                InsertCharacters("This is a searchable index. Enter search keywords:");

                            var input = HtmlToken.OpenTag(HTMLInputElement.Tag);
                            input.AddAttribute("name", HTMLIsIndexElement.Tag);

                            for (int i = 0; i < tag.Attributes.Count; i++)
                            {
                                if (tag.Attributes[i].Key == "name" || tag.Attributes[i].Key == "action" || tag.Attributes[i].Key == "prompt")
                                    continue;

                                input.AddAttribute(tag.Attributes[i].Key, tag.Attributes[i].Value);
                            }

                            InBody(input);
                            InBody(HtmlToken.CloseTag(HTMLLabelElement.Tag));
                            InBody(HtmlToken.OpenTag(HTMLHRElement.Tag));
                            InBody(HtmlToken.CloseTag(HTMLFormElement.Tag));
                        }
                        break;
                    }
                    case HTMLTextAreaElement.Tag:
                    {
                        var element = new HTMLTextAreaElement();
                        AddElementToCurrentNode(element, token);
                        tokenizer.Switch(HtmlParseMode.RCData);
                        originalInsert = insert;
                        frameset = false;
                        insert = HtmlTreeMode.Text;
                        PreventNewLine();
                        break;
                    }
                    case HTMLSemanticElement.XmpTag:
                    {
                        if (IsInButtonScope(HTMLParagraphElement.Tag))
                            InBodyEndTagParagraph();

                        ReconstructFormatting();
                        frameset = false;
                        RawtextAlgorithm(tag);
                        break;
                    }
                    case HTMLIFrameElement.Tag:
                    {
                        frameset = false;
                        RawtextAlgorithm(tag);
                        break;
                    }
                    case HTMLSelectElement.Tag:
                    {
                        ReconstructFormatting();
                        var element = new HTMLSelectElement();
                        AddElementToCurrentNode(element, token);
                        frameset = false;

                        switch (insert)
                        {
                            case HtmlTreeMode.InTable:
                            case HtmlTreeMode.InCaption:
                            case HtmlTreeMode.InRow:
                            case HtmlTreeMode.InCell:
                                insert = HtmlTreeMode.InSelectInTable;
                                break;

                            default:
                                insert = HtmlTreeMode.InSelect;
                                break;
                        }
                        break;
                    }
                    case HTMLOptGroupElement.Tag:
                    case HTMLOptionElement.Tag:
                    {
                        if (CurrentNode.NodeName == HTMLOptionElement.Tag)
                            InBodyEndTagAnythingElse(HtmlToken.CloseTag(HTMLOptionElement.Tag));

                        ReconstructFormatting();
                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case "rp":
                    case "rt":
                    {
                        if (IsInScope("ruby"))
                        {
                            GenerateImpliedEndTags();

                            if (CurrentNode.NodeName != "ruby")
                                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);
                        }
                            
                        var element = HTMLElement.Factory(tag.Name);
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case HTMLNoElement.NoEmbedTag:
                    {
                        RawtextAlgorithm(tag);
                        break;
                    }
                    case HTMLNoElement.NoScriptTag:
                    {
                        if (!doc.IsScripting)
                            goto default;

                        RawtextAlgorithm(tag);
                        break;
                    }
                    case MathMLElement.RootTag:
                    {
                        var element = new MathMLElement();
                        element.NodeName = tag.Name;
                        ReconstructFormatting();

                        for (int i = 0; i < tag.Attributes.Count; i++)
                        {
                            var name = tag.Attributes[i].Key;
                            var value = tag.Attributes[i].Value;
                            element.SetAttribute(ForeignHelpers.AdjustAttributeName(MathMLHelpers.AdjustAttributeName(name)), value);
                        }

                        CurrentNode.AppendChild(element);

                        if (!tag.IsSelfClosing)
                            open.Add(element);
                        break;
                    }
                    case SVGElement.RootTag:
                    {
                        var element = new SVGElement();
                        element.NodeName = tag.Name;
                        ReconstructFormatting();

                        for (int i = 0; i < tag.Attributes.Count; i++)
                        {
                            var name = tag.Attributes[i].Key;
                            var value = tag.Attributes[i].Value;
                            element.SetAttribute(ForeignHelpers.AdjustAttributeName(MathMLHelpers.AdjustAttributeName(name)), value);
                        }

                        CurrentNode.AppendChild(element);

                        if (!tag.IsSelfClosing)
                        {
                            open.Add(element);
                            tokenizer.AcceptsCharacterData = true;
                        }
                        break;
                    }
                    case HTMLTableCaptionElement.Tag:
                    case HTMLTableColElement.ColTag:
                    case HTMLTableColElement.ColgroupTag:
                    case HTMLFrameElement.Tag:
                    case HTMLHeadElement.Tag:
                    case HTMLTableSectionElement.BodyTag:
                    case HTMLTableCellElement.NormalTag:
                    case HTMLTableSectionElement.FootTag:
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableRowElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TagCannotStartHere);
                        break;
                    }
                    default:
                    {
                        ReconstructFormatting();
                        var element = new HTMLUnknownElement();
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLBodyElement.Tag:
                    {
                        InBodyEndTagBody();
                        break;
                    }
                    case HTMLHtmlElement.Tag:
                    {
                        if (InBodyEndTagBody())
                            AfterBody(token);

                        break;
                    }
                    case HTMLSemanticElement.AddressTag:
                    case HTMLSemanticElement.ArticleTag:
                    case HTMLSemanticElement.AsideTag:
                    case HTMLQuoteElement.BlockTag:
                    case HTMLButtonElement.Tag:
                    case HTMLSemanticElement.CenterTag:
                    case HTMLDetailsElement.Tag:
                    case HTMLDialogElement.Tag:
                    case HTMLDirectoryElement.Tag:
                    case HTMLDivElement.Tag:
                    case HTMLDListElement.Tag:
                    case HTMLFieldSetElement.Tag:
                    case HTMLSemanticElement.FigcaptionTag:
                    case HTMLSemanticElement.FigureTag:
                    case HTMLSemanticElement.FooterTag:
                    case HTMLSemanticElement.HeaderTag:
                    case HTMLSemanticElement.HgroupTag:
                    case HTMLSemanticElement.ListingTag:
                    case HTMLSemanticElement.MainTag:
                    case HTMLMenuElement.Tag:
                    case HTMLSemanticElement.NavTag:
                    case HTMLOListElement.Tag:
                    case HTMLPreElement.Tag:
                    case HTMLSemanticElement.SectionTag:
                    case HTMLSemanticElement.SummaryTag:
                    case HTMLUListElement.Tag:
                    {
                        InBodyEndTagBlock(tag.Name);
                        break;
                    }
                    case HTMLFormElement.Tag:
                    {
                        var node = form;
                        form = null;

                        if (node != null && IsInScope(node.NodeName))
                        {
                            GenerateImpliedEndTags();

                            if (CurrentNode != node)
                                RaiseErrorOccurred(ErrorCode.FormClosedWrong);

                            open.Remove(node);
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.FormNotInScope);

                        break;
                    }
                    case HTMLParagraphElement.Tag:
                    {
                        InBodyEndTagParagraph();
                        break;
                    }
                    case HTMLLIElement.ItemTag:
                    {
                        if (IsInListItemScope(tag.Name))
                        {
                            GenerateImpliedEndTagsExceptFor(tag.Name);

                            if (CurrentNode.NodeName != tag.Name)
                                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                            ClearStackBackTo(tag.Name);
                            CloseCurrentNode();
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.ListItemNotInScope);

                        break;
                    }
                    case HTMLLIElement.DefinitionTag:
                    case HTMLLIElement.DescriptionTag:
                    {
                        if (IsInScope(tag.Name))
                        {
                            GenerateImpliedEndTagsExceptFor(tag.Name);

                            if (CurrentNode.NodeName != tag.Name)
                                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                            ClearStackBackTo(tag.Name);
                            CloseCurrentNode();
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.ListItemNotInScope);

                        break;
                    }
                    case HTMLHeadingElement.ChapterTag:
                    case HTMLHeadingElement.SubSubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSectionTag:
                    case HTMLHeadingElement.SubSectionTag:
                    case HTMLHeadingElement.SectionTag:
                    {
                        if (IsHeadingInScope())
                        {
                            GenerateImpliedEndTags();

                            if (CurrentNode.NodeName != tag.Name)
                                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                            ClearStackBackToHeading();
                            CloseCurrentNode();
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.HeadingNotInScope);

                        break;
                    }
                    case HTMLAnchorElement.Tag:
                    case HTMLFormattingElement.BTag:
                    case HTMLFormattingElement.BigTag:
                    case HTMLFormattingElement.CodeTag:
                    case HTMLFormattingElement.EmTag:
                    case HTMLFontElement.Tag:
                    case HTMLFormattingElement.ITag:
                    case HTMLFormattingElement.NobrTag:
                    case HTMLFormattingElement.STag:
                    case HTMLFormattingElement.SmallTag:
                    case HTMLFormattingElement.StrikeTag:
                    case HTMLFormattingElement.StrongTag:
                    case HTMLFormattingElement.TtTag:
                    case HTMLFormattingElement.UTag:
                    {
                        HeisenbergAlgorithm(tag);
                        break;
                    }
                    case HTMLAppletElement.Tag:
                    case HTMLMarqueeElement.Tag:
                    case HTMLObjectElement.Tag:
                    {
                        if (IsInScope(tag.Name))
                        {
                            GenerateImpliedEndTags();

                            if (CurrentNode.NodeName != tag.Name)
                                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                            ClearStackBackTo(tag.Name);
                            CloseCurrentNode();
                            ClearFormattingElements();
                        }
                        else
                            RaiseErrorOccurred(ErrorCode.ObjectNotInScope);

                        break;
                    }
                    case HTMLBRElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                        InBodyStartTagBreakrow(HtmlToken.OpenTag(HTMLBRElement.Tag));
                        break;
                    }
                    default:
                    {
                        InBodyEndTagAnythingElse(tag);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                for (var i = 0; i < open.Count; i++)
                {
                    switch (open[i].NodeName)
                    {
                        case HTMLLIElement.DescriptionTag:
                        case HTMLLIElement.DefinitionTag:
                        case HTMLLIElement.ItemTag:
                        case HTMLParagraphElement.Tag:
                        case HTMLTableSectionElement.BodyTag:
                        case HTMLTableCellElement.HeadTag:
                        case HTMLTableSectionElement.FootTag:
                        case HTMLTableCellElement.NormalTag:
                        case HTMLTableSectionElement.HeadTag:
                        case HTMLTableRowElement.Tag:
                        case HTMLBodyElement.Tag:
                        case HTMLHtmlElement.Tag:
                            break;

                        default:
                            RaiseErrorOccurred(ErrorCode.BodyClosedWrong);
                            i = open.Count;
                            break;
                    }
                }

                End();
            }
        }

        /// <summary>
        /// See 8.2.5.4.8 The "text" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void Text(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
                InsertCharacters(((HtmlCharacterToken)token).Data);
            else if (token.Type == HtmlTokenType.EOF)
            {
                RaiseErrorOccurred(ErrorCode.EOF);
                CloseCurrentNode();
                insert = originalInsert;
                Consume(token);
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name == HTMLScriptElement.Tag)
                {
                    PerformMicrotaskCheckpoint();
                    ProvideStableState();
                    var script = (HTMLScriptElement)CurrentNode;
                    CloseCurrentNode();
                    insert = originalInsert;
                    var oldInsertion = tokenizer.Stream.InsertionPoint;
                    nesting++;
                    //script.Prepare();
                    nesting--;

                    if (nesting == 0)
                        pause = false;

                    tokenizer.Stream.InsertionPoint = oldInsertion;

                    if (pendingParsingBlock != null)
                    {
                        if (nesting != 0)
                        {
                            pause = true;
                            return;
                        }

                        do
                        {
                            script = pendingParsingBlock;
                            pendingParsingBlock = null;
                            //TODO Do not call Tokenizer HERE
                            //TODO
                            //    3. If the parser's Document has a style sheet that is blocking scripts or the script's "ready to be parser-executed"
                            //       flag is not set: spin the event loop until the parser's Document has no style sheet that is blocking scripts and
                            //       the script's "ready to be parser-executed" flag is set.
                            //TODO From here on Tokenizer can be called again
                            oldInsertion = tokenizer.Stream.InsertionPoint;
                            nesting++;
                            //script.Execute();
                            nesting--;

                            if (nesting == 0)
                                pause = false;

                            tokenizer.Stream.ResetInsertionPoint();
                        }
                        while (pendingParsingBlock != null);
                    }
                }
                else
                {
                    CloseCurrentNode();
                    insert = originalInsert;
                }
            }
        }

        /// <summary>
        /// See 8.2.5.4.9 The "in table" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InTable(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLTableCaptionElement.Tag:
                    {
                        ClearStackBackToTable();
                        InsertScopeMarker();
                        var element = new HTMLTableCaptionElement();
                        AddElementToCurrentNode(element, token);
                        insert = HtmlTreeMode.InCaption;
                        break;
                    }
                    case HTMLTableColElement.ColgroupTag:
                    {
                        ClearStackBackToTable();
                        var element = new HTMLTableColElement();
                        AddElementToCurrentNode(element, token);
                        insert = HtmlTreeMode.InColumnGroup;
                        break;
                    }
                    case HTMLTableColElement.ColTag:
                    {
                        InTable(HtmlToken.OpenTag(HTMLTableColElement.ColgroupTag));
                        InColumnGroup(token);
                        break;
                    }
                    case HTMLTableSectionElement.BodyTag:
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableSectionElement.FootTag:
                    {
                        ClearStackBackToTable();
                        var element = new HTMLTableSectionElement();
                        AddElementToCurrentNode(element, token);
                        insert = HtmlTreeMode.InTableBody;
                        break;
                    }
                    case HTMLTableCellElement.NormalTag:
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableRowElement.Tag:
                    {
                        InTable(HtmlToken.OpenTag(HTMLTableSectionElement.BodyTag));
                        InTableBody(token);
                        break;
                    }
                    case HTMLTableElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TableNesting);

                        if (InTableEndTagTable())
                            Consume(token);

                        break;
                    }
                    case HTMLScriptElement.Tag:
                    case HTMLStyleElement.Tag:
                    {
                        InHead(token);
                        break;
                    }
                    case HTMLInputElement.Tag:
                    {
                        if (tag.GetAttribute("type").Equals("hidden", StringComparison.OrdinalIgnoreCase))
                        {
                            RaiseErrorOccurred(ErrorCode.InputUnexpected);
                            var element = new HTMLInputElement();
                            AddElementToCurrentNode(element, token, true);
                            CloseCurrentNode();
                        }
                        else
                        {
                            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
                            InBodyWithFoster(token);
                        }

                        break;
                    }
                    case HTMLFormElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.FormInappropriate);

                        if (form == null)
                        {
                            var element = new HTMLFormElement();
                            AddElementToCurrentNode(element, token);
                            form = element;
                            CloseCurrentNode();
                        }

                        break;
                    }
                    default:
                    {
                        RaiseErrorOccurred(ErrorCode.IllegalElementInTableDetected);
                        InBodyWithFoster(token);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLTableElement.Tag:
                    {
                        InTableEndTagTable();
                        break;
                    }
                    case HTMLBodyElement.Tag:
                    case HTMLTableColElement.ColgroupTag:
                    case HTMLTableColElement.ColTag:
                    case HTMLTableCaptionElement.Tag:
                    case HTMLHtmlElement.Tag:
                    case HTMLTableSectionElement.BodyTag:
                    case HTMLTableRowElement.Tag:
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableSectionElement.FootTag:
                    case HTMLTableCellElement.NormalTag:
                    {
                        RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                        break;
                    }
                    default:
                    {
                        RaiseErrorOccurred(ErrorCode.IllegalElementInTableDetected);
                        InBodyWithFoster(token);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.Character && CurrentNode != null && CurrentNode.IsTableElement())
            {
                InTableText((HtmlCharacterToken)token);
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                if (CurrentNode != doc.DocumentElement)
                    RaiseErrorOccurred(ErrorCode.CurrentNodeIsNotRoot);

                End();
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TokenNotPossible);
                InBodyWithFoster(token);
            }
        }

        /// <summary>
        /// See 8.2.5.4.10 The "in table text" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InTableText(HtmlCharacterToken token)
        {
            var anyAreNotSpaceCharacters = token.HasContent;

            if (anyAreNotSpaceCharacters)
                RaiseErrorOccurred(ErrorCode.TokenNotPossible);

            if (anyAreNotSpaceCharacters)
                InBodyWithFoster(token);
            else
                InsertCharacters(token.Data);
        }

        /// <summary>
        /// See 8.2.5.4.11 The "in caption" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InCaption(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLTableCaptionElement.Tag:
                    {
                        InCaptionEndTagCaption();
                        break;
                    }
                    case HTMLBodyElement.Tag:
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableColElement.ColgroupTag:
                    case HTMLHtmlElement.Tag:
                    case HTMLTableSectionElement.BodyTag:
                    case HTMLTableColElement.ColTag:
                    case HTMLTableSectionElement.FootTag:
                    case HTMLTableCellElement.NormalTag:
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableRowElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                        break;
                    }
                    case HTMLTableElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.TableNesting);

                        if (InCaptionEndTagCaption())
                            InTable(token);

                        break;
                    }
                    default:
                    {
                        InBody(token);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLTableCaptionElement.Tag:
                    case HTMLTableColElement.ColTag:
                    case HTMLTableColElement.ColgroupTag:
                    case HTMLTableSectionElement.BodyTag:
                    case HTMLTableCellElement.NormalTag:
                    case HTMLTableSectionElement.FootTag:
                    case HTMLTableCellElement.HeadTag:
                    case HTMLTableSectionElement.HeadTag:
                    case HTMLTableRowElement.Tag: 
                        RaiseErrorOccurred(ErrorCode.TagCannotStartHere);

                        if (InCaptionEndTagCaption())
                            InTable(token);

                        break;

                    default:
                        InBody(token);
                        break;
                }
            }
            else
                InBody(token);
        }

        /// <summary>
        /// See 8.2.5.4.12 The "in column group" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InColumnGroup(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                var str = chars.TrimStart();
                InsertCharacters(str);
            }
            else if (token.Type == HtmlTokenType.Comment)
                AddComment(CurrentNode, token);
            else if (token.Type == HtmlTokenType.DOCTYPE)
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
            else if (token.IsStartTag(HTMLHtmlElement.Tag))
                InBody(token);
            else if (token.IsStartTag(HTMLTableColElement.ColTag))
            {
                var element = new HTMLTableColElement();
                AddElementToCurrentNode(element, token, true);
                CloseCurrentNode();
            }
            else if (token.IsEndTag(HTMLTableColElement.ColgroupTag))
                InColumnGroupEndTagColgroup();
            else if (token.IsEndTag(HTMLTableColElement.ColTag))
                RaiseErrorOccurred(ErrorCode.TagClosedWrong);
            else if (token.Type == HtmlTokenType.EOF && CurrentNode == doc.DocumentElement)
                End();
            else if (InColumnGroupEndTagColgroup())
                InTable(token);
        }

        /// <summary>
        /// See 8.2.5.4.13 The "in table body" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InTableBody(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name == HTMLTableRowElement.Tag)
                {
                    ClearStackBackToTableSection();
                    var element = new HTMLTableRowElement();
                    AddElementToCurrentNode(element, token);
                    insert = HtmlTreeMode.InRow;
                }
                else if (tag.Name.IsTableCellElement())
                {
                    InTableBody(HtmlToken.OpenTag(HTMLTableRowElement.Tag));
                    InRow(token);
                }
                else if (tag.Name.IsGeneralTableElement())
                {
                    InTableBodyCloseTable(tag);
                }
                else
                {
                    InTable(token);
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name.IsTableSectionElement())
                {
                    if (IsInTableScope(((HtmlTagToken)token).Name))
                    {
                        ClearStackBackToTableSection();
                        CloseCurrentNode();
                        insert = HtmlTreeMode.InTable;
                    }
                    else
                    {
                        RaiseErrorOccurred(ErrorCode.TableSectionNotInScope);
                    }
                }
                else if (tag.Name.IsSpecialTableElement(true))
                {
                    RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                }
                else if(tag.Name == HTMLTableElement.Tag)
                {
                    InTableBodyCloseTable(tag);
                }
                else
                {
                    InTable(token);
                }
            }
            else
            {
                InTable(token);
            }
        }

        /// <summary>
        /// See 8.2.5.4.14 The "in row" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InRow(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name.IsTableCellElement())
                {
                    ClearStackBackToTableRow();
                    var element = new HTMLTableCellElement();
                    AddElementToCurrentNode(element, token);
                    insert = HtmlTreeMode.InCell;
                    InsertScopeMarker();
                }
                else if (tag.Name.IsGeneralTableElement(true))
                {
                    if (InRowEndTagTablerow())
                        InTableBody(token);
                }
                else
                {
                    InTable(token);
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                if(tag.Name == HTMLTableRowElement.Tag)   
                {
                    InRowEndTagTablerow();
                }
                else if (tag.Name == HTMLTableElement.Tag)
                {
                    if (InRowEndTagTablerow())
                        InTableBody(token);
                }
                else if (tag.Name.IsTableSectionElement())
                {
                    if (IsInTableScope(tag.Name))
                    {
                        InRowEndTagTablerow();
                        InTableBody(token);
                    }
                    else
                    {
                        RaiseErrorOccurred(ErrorCode.TableSectionNotInScope);
                    }
                }
                else if (tag.Name.IsSpecialTableElement())
                {
                    RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                }
                else
                {
                    InTable(token);
                }
            }
            else
            {
                InTable(token);
            }
        }

        /// <summary>
        /// See 8.2.5.4.15 The "in cell" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InCell(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name.IsTableCellElement())
                {
                    InCellEndTagCell(((HtmlTagToken)token).Name);
                }
                else if (tag.Name.IsSpecialTableElement())
                {
                    RaiseErrorOccurred(ErrorCode.TagCannotEndHere);}
                else if (tag.Name.IsTableElement())
                {
                    if (IsInTableScope(tag.Name))
                    {
                        CloseTheCell();
                        Consume(token);
                    }
                    else
                    {
                        RaiseErrorOccurred(ErrorCode.TableNotInScope);
                    }
                }
                else
                {
                    InBody(token);
                }
            }
            else if (token.Type == HtmlTokenType.StartTag && (((HtmlTagToken)token).Name.IsGeneralTableElement(true) || ((HtmlTagToken)token).Name.IsTableCellElement()))
            {
                var tag = (HtmlTagToken)token;

                if (IsInTableScope(HTMLTableCellElement.NormalTag) || IsInTableScope(HTMLTableCellElement.HeadTag))
                {
                    CloseTheCell();
                    Consume(token);
                }
                else
                {
                    RaiseErrorOccurred(ErrorCode.TableCellNotInScope);
                }
            }
            else
            {
                InBody(token);
            }
        }

        /// <summary>
        /// See 8.2.5.4.16 The "in select" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InSelect(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                InsertCharacters(((HtmlCharacterToken)token).Data);
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLHtmlElement.Tag:
                    {
                        InBody(token);
                        break;
                    }
                    case HTMLOptionElement.Tag:
                    {
                        if (CurrentNode.NodeName == HTMLOptionElement.Tag)
                            InSelectEndTagOption();

                        var element = new HTMLOptionElement();
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case HTMLOptGroupElement.Tag:
                    {
                        if (CurrentNode.NodeName == HTMLOptionElement.Tag)
                            InSelectEndTagOption();

                        if (CurrentNode.NodeName == HTMLOptGroupElement.Tag)
                            InSelectEndTagOptgroup();

                        var element = new HTMLOptGroupElement();
                        AddElementToCurrentNode(element, token);
                        break;
                    }
                    case HTMLSelectElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.SelectNesting);
                        InSelectEndTagSelect();
                        break;
                    }
                    case HTMLInputElement.Tag:
                    case HTMLKeygenElement.Tag:
                    case HTMLTextAreaElement.Tag:
                    {
                        RaiseErrorOccurred(ErrorCode.IllegalElementInSelectDetected);

                        if (IsInSelectScope(HTMLSelectElement.Tag))
                        {
                            InSelectEndTagSelect();
                            Consume(token);
                        }

                        break;
                    }
                    case HTMLScriptElement.Tag:
                    {
                        InHead(token);
                        break;
                    }
                    default:
                    {
                        RaiseErrorOccurred(ErrorCode.IllegalElementInSelectDetected);
                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLOptGroupElement.Tag:
                        InSelectEndTagOptgroup();
                        break;
                    case HTMLOptionElement.Tag:
                        InSelectEndTagOption();
                        break;
                    case HTMLSelectElement.Tag:
                        if (IsInSelectScope(HTMLSelectElement.Tag))
                            InSelectEndTagSelect();
                        else
                            RaiseErrorOccurred(ErrorCode.SelectNotInScope);

                        break;
                    default:
                        RaiseErrorOccurred(ErrorCode.TagCannotEndHere);
                        break;
                }
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                if (CurrentNode != doc.DocumentElement)
                    RaiseErrorOccurred(ErrorCode.CurrentNodeIsNotRoot);

                End();
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TokenNotPossible);
            }
        }

        /// <summary>
        /// See 8.2.5.4.17 The "in select in table" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InSelectInTable(HtmlToken token)
        {
            var tag = token as HtmlTagToken;

            if (tag != null && (tag.Name.IsTableCellElement() || tag.Name.IsTableElement() || tag.Name == HTMLTableCaptionElement.Tag))
            {
                if (token.Type == HtmlTokenType.StartTag)
                {
                    RaiseErrorOccurred(ErrorCode.IllegalElementInSelectDetected);
                    InSelectEndTagSelect();
                    Consume(token);
                }
                else
                {
                    RaiseErrorOccurred(ErrorCode.TagCannotEndHere);

                    if (IsInTableScope(tag.Name))
                    {
                        InSelectEndTagSelect();
                        Consume(token);
                    }
                }
            }
            else
            {
                InSelect(token);
            }
        }

        /// <summary>
        /// See 8.2.5.4.18 The "after body" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void AfterBody(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chars = (HtmlCharacterToken)token;
                var str = chars.TrimStart();
                ReconstructFormatting();
                InsertCharacters(str);

                if (chars.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(open[0], token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if(token.IsTag(HTMLHtmlElement.Tag))
            {
                if (token.Type == HtmlTokenType.StartTag)
                    InBody(token);
                else if (IsFragmentCase)
                    RaiseErrorOccurred(ErrorCode.TagInvalidInFragmentMode);
                else
                    insert = HtmlTreeMode.AfterAfterBody;

                return;
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                End();
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
            insert = HtmlTreeMode.InBody;
            InBody(token);
        }

        /// <summary>
        /// See 8.2.5.4.19 The "in frameset" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void InFrameset(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                var str = chrs.TrimStart();
                InsertCharacters(str);

                if (chrs.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name == HTMLHtmlElement.Tag)
                {
                    InBody(token);
                    return;
                }
                else if (tag.Name == HTMLFrameSetElement.Tag)
                {
                    var element = new HTMLFrameSetElement();
                    AddElementToCurrentNode(element, token);
                    return;
                }
                else if (tag.Name == HTMLFrameElement.Tag)
                {
                    var element = new HTMLFrameElement();
                    AddElementToCurrentNode(element, token, true);
                    CloseCurrentNode();
                    return;
                }
                else if (tag.Name == HTMLNoElement.NoFramesTag)
                {
                    InHead(token);
                    return;
                }
            }
            else if (token.Type == HtmlTokenType.EndTag && ((HtmlTagToken)token).Name == HTMLFrameSetElement.Tag)
            {
                if (CurrentNode != doc.DocumentElement)
                {
                    CloseCurrentNode();

                    if (IsFragmentCase && CurrentNode.NodeName != HTMLFrameSetElement.Tag)
                        insert = HtmlTreeMode.AfterFrameset;
                }
                else
                    RaiseErrorOccurred(ErrorCode.CurrentNodeIsRoot);

                return;
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                if (CurrentNode != doc.DocumentElement)
                    RaiseErrorOccurred(ErrorCode.CurrentNodeIsNotRoot);

                End();
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
        }

        /// <summary>
        /// See 8.2.5.4.20 The "after frameset" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void AfterFrameset(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                var str = chrs.TrimStart();
                InsertCharacters(str);

                if (chrs.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
                return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
                return;
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                if (tag.Name == HTMLHtmlElement.Tag)
                {
                    InBody(token);
                    return;
                }
                else if (tag.Name == HTMLNoElement.NoFramesTag)
                {
                    InHead(token);
                    return;
                }
            }
            else if (token.Type == HtmlTokenType.EndTag && ((HtmlTagToken)token).Name == HTMLHtmlElement.Tag)
            {
                insert = HtmlTreeMode.AfterAfterFrameset;
                return;
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                End();
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
        }

        /// <summary>
        /// See 8.2.5.4.21 The "after after body" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void AfterAfterBody(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(doc, token);
                return;
            }
            else if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                var str = chrs.TrimStart();
                ReconstructFormatting();
                InsertCharacters(str);

                if (chrs.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE || token.IsStartTag(HTMLHtmlElement.Tag))
            {
                InBody(token);
                return;
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                End();
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
            insert = HtmlTreeMode.InBody;
            InBody(token);
        }

        /// <summary>
        /// See 8.2.5.4.22 The "after after frameset" insertion mode.
        /// </summary>
        /// <param name="token">The passed token.</param>
        void AfterAfterFrameset(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(doc, token);
                return;
            }
            else if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                var str = chrs.TrimStart();
                ReconstructFormatting();
                InsertCharacters(str);

                if (chrs.IsEmpty)
                    return;
            }
            else if (token.Type == HtmlTokenType.DOCTYPE || token.IsStartTag(HTMLHtmlElement.Tag))
            {
                InBody(token);
                return;
            }
            else if (token.IsStartTag(HTMLNoElement.NoFramesTag))
            {
                InHead(token);
                return;
            }
            else if (token.Type == HtmlTokenType.EOF)
            {
                End();
                return;
            }

            RaiseErrorOccurred(ErrorCode.TokenNotPossible);
        }

        #endregion

        #region Substates

        /// <summary>
        /// Closes the table if the section is in table scope.
        /// </summary>
        /// <param name="tag">The tag to insert which triggers the closing of the table.</param>
        void InTableBodyCloseTable(HtmlTagToken tag)
        {
            if (IsSectionInTableScope())
            {
                ClearStackBackToTableSection();
                CloseCurrentNode();
                insert = HtmlTreeMode.InTable;
                InTable(tag);
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TableSectionNotInScope);
            }
        }

        /// <summary>
        /// Acts if a option end tag had been seen in the InSelect state.
        /// </summary>
        void InSelectEndTagOption()
        {
            if (CurrentNode.NodeName == HTMLOptionElement.Tag)
                CloseCurrentNode();
            else
                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);
        }

        /// <summary>
        /// Acts if a optgroup end tag had been seen in the InSelect state.
        /// </summary>
        void InSelectEndTagOptgroup()
        {
            if (open.Count > 1 && open[open.Count - 1] is HTMLOptionElement && open[open.Count - 2] is HTMLOptGroupElement)
                CloseCurrentNode();

            if (CurrentNode is HTMLOptGroupElement)
                CloseCurrentNode();
            else
                RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);
        }

        /// <summary>
        /// Act as if an colgroup end tag has been found in the InColumnGroup state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        bool InColumnGroupEndTagColgroup()
        {
            if (CurrentNode != doc.DocumentElement)
            {
                CloseCurrentNode();
                insert = HtmlTreeMode.InTable;
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.CurrentNodeIsRoot);
                return false;
            }
        }

        /// <summary>
        /// Act as if a body start tag has been found in the AfterHead state.
        /// </summary>
        /// <param name="token"></param>
        void AfterHeadStartTagBody(HtmlTagToken token)
        {
            var element = new HTMLBodyElement();
            AddElementToCurrentNode(element, token);
            frameset = false;
            insert = HtmlTreeMode.InBody;
        }

        /// <summary>
        /// Follows the generic rawtext parsing algorithm.
        /// </summary>
        /// <param name="tag">The given tag token.</param>
        void RawtextAlgorithm(HtmlTagToken tag)
        {
            var element = HTMLElement.Factory(tag.Name);
            AddElementToCurrentNode(element, tag);
            originalInsert = insert;
            insert = HtmlTreeMode.Text;
            tokenizer.Switch(HtmlParseMode.Rawtext);
        }

        /// <summary>
        /// Follows the generic RCData parsing algorithm.
        /// </summary>
        /// <param name="tag">The given tag token.</param>
        void RCDataAlgorithm(HtmlTagToken tag)
        {
            var element = HTMLElement.Factory(tag.Name);
            AddElementToCurrentNode(element, tag);
            originalInsert = insert;
            insert = HtmlTreeMode.Text;
            tokenizer.Switch(HtmlParseMode.RCData);
        }

        /// <summary>
        /// Acts if a li start tag in the InBody state has been found.
        /// </summary>
        /// <param name="tag">The actual tag given.</param>
        void InBodyStartTagListItem(HtmlTagToken tag)
        {
            frameset = false;
            var index = open.Count - 1;
            var node = open[index];

            while (true)
            {
                if (node.NodeName == HTMLLIElement.ItemTag)
                {
                    InBody(HtmlToken.CloseTag(node.NodeName));
                    break;
                }

                if (node.IsSpecial && node.NodeName != HTMLSemanticElement.AddressTag && !(node is HTMLDivElement) && !(node is HTMLParagraphElement))
                    break;
                
                node = open[--index];
            }

            if (IsInButtonScope(HTMLParagraphElement.Tag))
                InBodyEndTagParagraph();

            var element = HTMLElement.Factory(tag.Name);
            AddElementToCurrentNode(element, tag);
        }

        /// <summary>
        /// Acts if a dd or dt start tag in the InBody state has been found.
        /// </summary>
        /// <param name="tag">The actual tag given.</param>
        void InBodyStartTagDefinitionItem(HtmlTagToken tag)
        {
            frameset = false;
            var index = open.Count - 1;
            var node = open[index];

            while (true)
            {
                if (node.NodeName == HTMLLIElement.DefinitionTag || node.NodeName == HTMLLIElement.DescriptionTag)
                {
                    InBody(HtmlToken.CloseTag(node.NodeName));
                    break;
                }

                if (node.IsSpecial && node.NodeName != HTMLSemanticElement.AddressTag && !(node is HTMLDivElement) && !(node is HTMLParagraphElement))
                    break;

                node = open[--index];
            }

            if (IsInButtonScope(HTMLParagraphElement.Tag))
                InBodyEndTagParagraph();

            var element = HTMLElement.Factory(tag.Name);
            AddElementToCurrentNode(element, tag);
        }

        /// <summary>
        /// Acts if a button or similar end tag had been seen in the InBody state.
        /// </summary>
        /// <param name="tagName">The name of the block element.</param>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        bool InBodyEndTagBlock(string tagName)
        {
            if (IsInScope(tagName))
            {
                GenerateImpliedEndTags();

                if (CurrentNode.NodeName != tagName)
                    RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                ClearStackBackTo(tagName);
                CloseCurrentNode();
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.BlockNotInScope);
                return false;
            }
        }

        /// <summary>
        /// Acts if a nobr tag had been seen in the InBody state.
        /// </summary>
        /// <param name="tag">The actual tag given.</param>
        void HeisenbergAlgorithm(HtmlTagToken tag)
        {
            var outer = 0;
            var inner = 0;
            var bookmark = 0;
            var index = 0;

            Element formattingElement;
            Element furthestBlock;
            Element commonAncestor;
            Element node;
            Element lastNode;

            while(outer < 8)
            {
                outer++;
                index = 0;
                formattingElement = null;

                for (var j = formatting.Count - 1; j >= 0; j--)
                {
                    if (formatting[j] is ScopeMarkerNode)
                        break;
                    else if (formatting[j].NodeName == tag.Name)
                    {
                        index = j;
                        formattingElement = formatting[j];
                        break;
                    }
                }

                if (formattingElement == null)
                {
                    InBodyEndTagAnythingElse(tag);
                    break;
                }

                var openIndex = open.IndexOf(formattingElement);

                if (openIndex == -1)
                {
                    RaiseErrorOccurred(ErrorCode.FormattingElementNotFound);
                    formatting.Remove(formattingElement);
                    break;
                }

                if (!IsInScope(formattingElement.NodeName))
                {
                    RaiseErrorOccurred(ErrorCode.ElementNotInScope);
                    break;
                }

                if (openIndex != open.Count - 1)
                    RaiseErrorOccurred(ErrorCode.TagClosedWrong);

                furthestBlock = null;
                bookmark = index;

                for (var j = openIndex + 1; j < open.Count; j++)
                {
                    if (open[j].IsSpecial)
                    {
                        index = j;
                        furthestBlock = open[j];
                        break;
                    }
                }

                if (furthestBlock == null)
                {
                    do
                    {
                        furthestBlock = CurrentNode;
                        CloseCurrentNode();
                    }
                    while (furthestBlock != formattingElement);

                    formatting.Remove(formattingElement);
                    break;
                }

                commonAncestor = open[openIndex - 1];
                inner = 0;
                node = furthestBlock;
                lastNode = furthestBlock;

                while (true)
                {
                    inner++;
                    node = open[--index];

                    if (node == formattingElement)
                        break;

                    if (inner > 3 && formatting.Contains(node))
                        formatting.Remove(node);

                    if (!formatting.Contains(node))
                    {
                        open.Remove(node);
                        continue;
                    }

                    var newElement = CopyElement(node);
                    commonAncestor.AppendChild(newElement);

                    open[index] = newElement;
                    
                    for(var l = 0; l != formatting.Count; l++)
                    {
                        if(formatting[l] == node)
                        {
                            formatting[l] = newElement;
                            break;
                        }
                    }

                    node = newElement;

                    if (lastNode == furthestBlock)
                        bookmark++;

                    if(lastNode.ParentNode != null)
                        lastNode.ParentNode.RemoveChild(lastNode);

                    node.AppendChild(lastNode);
                    lastNode = node;
                }

                if (commonAncestor.IsTableElement())
                    AddElementWithFoster(lastNode);
                else
                {
                    if (lastNode.ParentNode != null)
                        lastNode.ParentNode.RemoveChild(lastNode);

                    commonAncestor.AppendChild(lastNode);
                }

                var element = CopyElement(formattingElement);

                while(furthestBlock.ChildNodes.Length > 0)
                    element.AppendChild(furthestBlock.RemoveChild(furthestBlock.ChildNodes[0]));

                furthestBlock.AppendChild(element);
                formatting.Remove(formattingElement);
                formatting.Insert(bookmark, element);
                open.Remove(formattingElement);
                open.Insert(open.IndexOf(furthestBlock) + 1, element);
            }
        }

        /// <summary>
        /// Copies the element and its attributes to create a new element.
        /// </summary>
        /// <param name="element">The old element (source).</param>
        /// <returns>The new element (target).</returns>
        Element CopyElement(Element element)
        {
            var newElement = HTMLElement.Factory(element.NodeName);
            newElement.NodeName = element.NodeName;
            
            for (int i = 0; i < element.Attributes.Length; i++)
            {
                var attr = element.Attributes[i];
                newElement.SetAttribute(attr.NodeName, attr.NodeValue);
            }

            return newElement;
        }

        /// <summary>
        /// Performs the InBody state with foster parenting.
        /// </summary>
        /// <param name="token">The given token.</param>
        void InBodyWithFoster(HtmlToken token)
        {
            foster = true;
            InBody(token);
            foster = false;
        }

        /// <summary>
        /// Act as if an anything else end tag has been found in the InBody state.
        /// </summary>
        /// <param name="tag">The actual tag found.</param>
        void InBodyEndTagAnythingElse(HtmlTagToken tag)
        {
            var index = open.Count - 1;
            var node = CurrentNode;

            do
            {
                if (node.NodeName == tag.Name)
                {
                    GenerateImpliedEndTagsExceptFor(tag.Name);

                    if (node.NodeName == tag.Name)
                        RaiseErrorOccurred(ErrorCode.TagClosedWrong);

                    for (int i = open.Count - 1; index <= i; i--)
                        CloseCurrentNode();

                    break;
                }
                else if (node.IsSpecial)
                {
                    RaiseErrorOccurred(ErrorCode.TagClosedWrong);
                    break;
                }

                node = open[--index];
            }
            while (true);
        }

        /// <summary>
        /// Act as if an body end tag has been found in the InBody state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        bool InBodyEndTagBody()
        {
            if (IsInScope(HTMLBodyElement.Tag))
            {
                for (var i = 0; i < open.Count; i++)
                {
                    switch (open[i].NodeName)
                    {
                        case HTMLLIElement.DefinitionTag:
                        case HTMLLIElement.DescriptionTag:
                        case HTMLLIElement.ItemTag:
                        case HTMLOptGroupElement.Tag:
                        case HTMLOptionElement.Tag:
                        case HTMLParagraphElement.Tag:
                        case "rp":
                        case "rt":
                        case HTMLTableSectionElement.BodyTag:
                        case HTMLTableCellElement.NormalTag:
                        case HTMLTableSectionElement.FootTag:
                        case HTMLTableCellElement.HeadTag:
                        case HTMLTableSectionElement.HeadTag:
                        case HTMLTableRowElement.Tag:
                        case HTMLBodyElement.Tag:
                        case HTMLHtmlElement.Tag:
                            break;

                        default:
                            RaiseErrorOccurred(ErrorCode.BodyClosedWrong);
                            i = open.Count;
                            break;
                    }
                }

                insert = HtmlTreeMode.AfterBody;
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.BodyNotInScope);
                return false;
            }
        }

        /// <summary>
        /// Act as if an br start tag has been found in the InBody state.
        /// </summary>
        /// <param name="tag">The actual tag found.</param>
        void InBodyStartTagBreakrow(HtmlTagToken tag)
        {
            ReconstructFormatting();
            var element = HTMLElement.Factory(tag.Name);
            AddElementToCurrentNode(element, tag);
            CloseCurrentNode();
            frameset = false;
        }

        /// <summary>
        /// Act as if an p end tag has been found in the InBody state.
        /// </summary>
        /// <returns>True if the token was found, otherwise false.</returns>
        Boolean InBodyEndTagParagraph()
        {
            if (IsInButtonScope(HTMLParagraphElement.Tag))
            {
                GenerateImpliedEndTagsExceptFor(HTMLParagraphElement.Tag);

                if (CurrentNode.NodeName != HTMLParagraphElement.Tag)
                    RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                ClearStackBackTo(HTMLParagraphElement.Tag);
                CloseCurrentNode();
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.ParagraphNotInScope);
                InBody(HtmlToken.OpenTag(HTMLParagraphElement.Tag));
                InBodyEndTagParagraph();
                return false;
            }
        }

        /// <summary>
        /// Act as if an table end tag has been found in the InTable state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        Boolean InTableEndTagTable()
        {
            if (IsInTableScope(HTMLTableElement.Tag))
            {
                ClearStackBackTo(HTMLTableElement.Tag);
                CloseCurrentNode();
                Reset();
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TableNotInScope);
                return false;
            }
        }

        /// <summary>
        /// Act as if an tr end tag has been found in the InRow state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        Boolean InRowEndTagTablerow()
        {
            if (IsInTableScope(HTMLTableRowElement.Tag))
            {
                ClearStackBackToTableRow();
                CloseCurrentNode();
                insert = HtmlTreeMode.InTableBody;
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TableRowNotInScope);
                return false;
            }
        }

        /// <summary>
        /// Act as if an select end tag has been found in the InSelect state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        void InSelectEndTagSelect()
        {
            ClearStackBackTo(HTMLSelectElement.Tag);
            CloseCurrentNode();
            Reset();
        }

        /// <summary>
        /// Act as if an caption end tag has been found in the InCaption state.
        /// </summary>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        Boolean InCaptionEndTagCaption()
        {
            if (IsInTableScope(HTMLTableCaptionElement.Tag))
            {
                GenerateImpliedEndTags();

                if (CurrentNode.NodeName != HTMLTableCaptionElement.Tag)
                    RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                ClearStackBackTo(HTMLTableCaptionElement.Tag);
                CloseCurrentNode();
                ClearFormattingElements();
                insert = HtmlTreeMode.InTable;
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.CaptionNotInScope);
                return false;
            }
        }

        /// <summary>
        /// Act as if an td or th end tag has been found in the InCell state.
        /// </summary>
        /// <param name="tagName">The tag name (td or th) that has been found.</param>
        /// <returns>True if the token was not ignored, otherwise false.</returns>
        Boolean InCellEndTagCell(string tagName)
        {
            if (IsInTableScope(tagName))
            {
                GenerateImpliedEndTags();

                if (CurrentNode.NodeName != tagName)
                    RaiseErrorOccurred(ErrorCode.TagDoesNotMatchCurrentNode);

                ClearStackBackTo(tagName);
                CloseCurrentNode();
                ClearFormattingElements();
                insert = HtmlTreeMode.InRow;
                return true;
            }
            else
            {
                RaiseErrorOccurred(ErrorCode.TableCellNotInScope);
                return false;
            }
        }

        #endregion

        #region Foreign Content

        /// <summary>
        /// 8.2.5.5 The rules for parsing tokens in foreign content
        /// </summary>
        /// <param name="token">The token to examine.</param>
        void Foreign(HtmlToken token)
        {
            if (token.Type == HtmlTokenType.Character)
            {
                var chrs = (HtmlCharacterToken)token;
                InsertCharacters(chrs.Data.Replace(Specification.NULL, Specification.REPLACEMENT));

                if(chrs.HasContent)
                    frameset = false;
            }
            else if (token.Type == HtmlTokenType.Comment)
            {
                AddComment(CurrentNode, token);
            }
            else if (token.Type == HtmlTokenType.DOCTYPE)
            {
                RaiseErrorOccurred(ErrorCode.DoctypeTagInappropriate);
            }
            else if (token.Type == HtmlTokenType.StartTag)
            {
                var tag = (HtmlTagToken)token;

                switch (tag.Name)
                {
                    case HTMLFormattingElement.BTag:
                    case HTMLFormattingElement.BigTag:
                    case HTMLQuoteElement.BlockTag:
                    case HTMLBodyElement.Tag:
                    case HTMLBRElement.Tag:
                    case HTMLSemanticElement.CenterTag:
                    case HTMLFormattingElement.CodeTag:
                    case HTMLLIElement.DefinitionTag:
                    case HTMLDivElement.Tag:
                    case HTMLDListElement.Tag:
                    case HTMLLIElement.DescriptionTag:
                    case HTMLFormattingElement.EmTag:
                    case HTMLEmbedElement.Tag:
                    case HTMLHeadingElement.ChapterTag:
                    case HTMLHeadingElement.SubSubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSubSectionTag:
                    case HTMLHeadingElement.SubSubSectionTag:
                    case HTMLHeadingElement.SubSectionTag:
                    case HTMLHeadingElement.SectionTag:
                    case HTMLHeadElement.Tag:
                    case HTMLHRElement.Tag:
                    case HTMLFormattingElement.ITag:
                    case HTMLImageElement.Tag:
                    case HTMLLIElement.ItemTag:
                    case HTMLSemanticElement.ListingTag:
                    case HTMLSemanticElement.MainTag:
                    case HTMLMenuElement.Tag:
                    case HTMLMetaElement.Tag:
                    case HTMLFormattingElement.NobrTag:
                    case HTMLOListElement.Tag:
                    case HTMLParagraphElement.Tag:
                    case HTMLPreElement.Tag:
                    case "ruby":
                    case HTMLFormattingElement.STag:
                    case HTMLFormattingElement.SmallTag:
                    case HTMLSpanElement.Tag:
                    case HTMLFormattingElement.StrongTag:
                    case HTMLFormattingElement.StrikeTag:
                    case "sub":
                    case "sup":
                    case HTMLTableElement.Tag:
                    case HTMLFormattingElement.TtTag:
                    case HTMLFormattingElement.UTag:
                    case HTMLUListElement.Tag:
                    case "var":
                    {
                        RaiseErrorOccurred(ErrorCode.TagCannotStartHere);
                        CloseCurrentNode();

                        while (!CurrentNode.IsHtmlTIP && !CurrentNode.IsMathMLTIP && !CurrentNode.IsInHtml)
                            CloseCurrentNode();

                        Consume(token);
                        break;
                    }
                    case HTMLFontElement.Tag:
                    {
                        for (var i = 0; i != tag.Attributes.Count; i++)
                        {
                            if (tag.Attributes[i].Key == "color" || tag.Attributes[i].Key == "face" || tag.Attributes[i].Key == "size")
                                goto case "var";
                        }

                        goto default;
                    }
                    default:
                    {
                        Element node;

                        if (AdjustedCurrentNode.IsInMathML)
                        {
                            node = new MathMLElement();
                            node.NodeName = tag.Name;

                            for (int i = 0; i < tag.Attributes.Count; i++)
                            {
                                var name = tag.Attributes[i].Key;
                                var value = tag.Attributes[i].Value;
                                node.SetAttribute(ForeignHelpers.AdjustAttributeName(MathMLHelpers.AdjustAttributeName(name)), value);
                            }
                        }
                        else if (AdjustedCurrentNode.IsInSvg)
                        {
                            node = new SVGElement();
                            node.NodeName = SVGHelpers.AdjustTagName(tag.Name);

                            for (int i = 0; i < tag.Attributes.Count; i++)
                            {
                                var name = tag.Attributes[i].Key;
                                var value = tag.Attributes[i].Value;
                                node.SetAttribute(ForeignHelpers.AdjustAttributeName(SVGHelpers.AdjustAttributeName(name)), value);
                            }
                        }
                        else
                            break;

                        node.NamespaceURI = AdjustedCurrentNode.NamespaceURI;
                        CurrentNode.AppendChild(node);
                        open.Add(node);

                        if (!tag.IsSelfClosing)
                            tokenizer.AcceptsCharacterData = true;
                        else if (tag.Name == HTMLScriptElement.Tag)
                            Foreign(HtmlToken.CloseTag(HTMLScriptElement.Tag));

                        break;
                    }
                }
            }
            else if (token.Type == HtmlTokenType.EndTag)
            {
                var tag = (HtmlTagToken)token;

                if (CurrentNode != null && CurrentNode is HTMLScriptElement && tag.Name == HTMLScriptElement.Tag)
                {
                    CloseCurrentNode();
                    var oldInsert = tokenizer.Stream.InsertionPoint;
                    nesting++;
                    pause = true;
                    InSvg(tag);
                    nesting--;

                    if (nesting == 0)
                        pause = false;

                    tokenizer.Stream.InsertionPoint = oldInsert;
                }
                else
                {
                    var node = CurrentNode;

                    if (node.NodeName != tag.Name)
                        RaiseErrorOccurred(ErrorCode.TagClosingMismatch);

                    while (open.Count > 0)
                    {
                        open.RemoveAt(open.Count - 1);

                        if (node.NodeName.ToLower() == tag.Name)
                            break;

                        node = CurrentNode;

                        if (node == null || node.IsInHtml)
                            break;
                    }

                    Reset();
                    Consume(token);
                }
            }
        }

        /// <summary>
        /// Processes the element according to the SVG rules.
        /// </summary>
        /// <param name="tag">The tag to process.</param>
        void InSvg(HtmlTagToken tag)
        {
            //TODO
            //Process the script element according to the SVG rules, if the user agent supports SVG. [SVG]
        }

        #endregion

        #region Scope

        /// <summary>
        /// Determines if one of the tag names (h1, h2, h3, h4, h5, h6) is in the global scope.
        /// </summary>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsHeadingInScope()
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node is HTMLHeadingElement)
                    return true;

                if (node.IsInHtml)
                {
                    switch (node.NodeName)
                    {
                        case HTMLMarqueeElement.Tag:
                        case HTMLObjectElement.Tag:
                        case HTMLTableCellElement.HeadTag:
                        case HTMLTableCellElement.NormalTag:
                        case HTMLHtmlElement.Tag:
                        case HTMLTableElement.Tag:
                        case HTMLTableCaptionElement.Tag:
                        case HTMLAppletElement.Tag:
                            return false;
                    }
                }
                else if (node.IsInSvg)
                {
                    if (node.IsSpecial)
                        return false;
                }
                else if (node.IsInMathML)
                {
                    if (node.IsSpecial)
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the given tag name is in the global scope.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsInScope(String tagName)
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node.NodeName == tagName)
                    return true;

                if (node.IsInHtml)
                {
                    switch (node.NodeName)
                    {
                        case HTMLMarqueeElement.Tag:
                        case HTMLObjectElement.Tag:
                        case HTMLTableCellElement.HeadTag:
                        case HTMLTableCellElement.NormalTag:
                        case HTMLHtmlElement.Tag:
                        case HTMLTableElement.Tag:
                        case HTMLTableCaptionElement.Tag:
                        case HTMLAppletElement.Tag:
                            return false;
                    }
                }
                else if (node.IsInSvg)
                {
                    if (node.IsSpecial)
                        return false;
                }
                else if (node.IsInMathML)
                {
                    if(node.IsSpecial)
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the given tag name is in the list scope.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsInListItemScope(String tagName)
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node.NodeName == tagName)
                    return true;

                if (node is HTMLUListElement || node is HTMLOListElement)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Determines if the given tag name is in the button scope.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsInButtonScope(String tagName)
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node.NodeName == tagName)
                    return true;

                if (node is HTMLButtonElement)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Determines if one of the tag names (tbody, tfoot, thead) is in the table scope.
        /// </summary>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsSectionInTableScope()
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node is HTMLTableSectionElement || node is HTMLTableSectionElement || node is HTMLTableSectionElement)
                    return true;

                if (node is HTMLHtmlElement || node is HTMLTableElement)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Determines if the given tag name is in the table scope.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsInTableScope(String tagName)
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node.NodeName == tagName)
                    return true;
                
                if (node is HTMLHtmlElement || node is HTMLTableElement)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Determines if the given tag name is in the select scope.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if it is in scope, otherwise false.</returns>
        Boolean IsInSelectScope(String tagName)
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var node = open[i];

                if (node.NodeName == tagName)
                    return true;

                if (!(node is HTMLOptGroupElement) && !(node is HTMLOptionElement))
                    return false;
            }

            return false;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the next token and removes the starting newline, if it has one.
        /// </summary>
        void PreventNewLine()
        {
            var temp = tokenizer.Get();

            if (temp.Type == HtmlTokenType.Character)
                ((HtmlCharacterToken)temp).RemoveNewLine();

            Consume(temp);
        }

        /// <summary>
        /// Resolves the encoding from the given charset and sets it.
        /// </summary>
        /// <param name="charset">The charset string.</param>
        void SetCharset(String charset)
        {
            var enc = HtmlEncoding.Resolve(charset);

            if (enc != null)
            {
                doc.InputEncoding = enc.WebName;
                tokenizer.Stream.Encoding = enc;
            }
        }

        void PerformMicrotaskCheckpoint()
        {
            //TODO
            //IF RUNNING MUTATION OBSERVERS == false
            //1. Let the running mutation observers flag be true.
            //2. Sort the tables with pending sorts.
            //3. Invoke MutationObserver objects for the unit of related similar-origin browsing contexts to which the script's browsing context belongs.
            //   ( Note: This will typically invoke scripted callbacks, which calls the jump to a code entry-point algorithm, which calls this perform a )
            //   ( microtask checkpoint algorithm again, which is why we use the running mutation observers flag to avoid reentrancy.                    )
            //4. Let the running mutation observers flag be false.
        }

        void ProvideStableState()
        {
            //When the user agent is to provide a stable state, if any asynchronously-running algorithms are awaiting a stable state, then
            //the user agent must run their synchronous section and then resume running their asynchronous algorithm (if appropriate).
        }

        /// <summary>
        /// 8.2.6 The end.
        /// </summary>
        void End()
        {
            doc.ReadyState = Readiness.Interactive;

            while (open.Count != 0)
                CloseCurrentNode();

            if (doc.ScriptsWaiting != 0)
            {
                //3.1 Spin the event loop until the first script in the list of scripts that will execute when the document has finished parsing
                //    has its "ready to be parser-executed" flag set and the parser's Document has no style sheet that is blocking scripts.
                //3.2 Execute the first script in the list of scripts that will execute when the document has finished parsing.
                //3.3 Remove the first script element from the list of scripts that will execute when the document has finished parsing (i.e. shift out the first entry in the list).
                //3.4 If the list of scripts that will execute when the document has finished parsing is still not empty, repeat these substeps again from substep 1.
            }

            doc.QueueTask(doc.RaiseDomContentLoaded);

            while (doc.ScriptsAsSoonAsPossible != 0)
                doc.SpinEventLoop();

            while (doc.IsLoadingDelayed)
                doc.SpinEventLoop();

            doc.QueueTask(() =>
            {
                doc.ReadyState = Readiness.Complete;
                //7.2 If the Document is in a browsing context, fire a simple event named load at the Document's Window object, but with its target set to the Document object
                //    (and the currentTarget set to the Window object).
            });

            if (doc.IsInBrowsingContext)
            {
                //8. If the Document is in a browsing context, then queue a task to run the following substeps:
                //8.1 If the Document's page showing flag is true, then abort this task (i.e. don't fire the event below).
                //8.2 Set the Document's page showing flag to true.
                //8.3 Fire a trusted event with the name pageshow at the Window object of the Document, but with its target set to the Document object (and the currentTarget set
                //    to the Window object), using the PageTransitionEvent interface, with the persisted attribute initialized to false. This event must not bubble, must not be
                //    cancelable, and has no default action.
            }

            //9. If the Document has any pending application cache download process tasks, then queue each such task in the order they were added to the list of pending
            //   application cache download process tasks, and then empty the list of pending application cache download process tasks. The task source for these tasks is
            //   the networking task source.

            if (doc.IsToBePrinted)
                doc.Print();

            //11. The Document is now ready for post-load tasks.
            doc.QueueTask(doc.RaiseLoadedEvent);

            if (IsAsync) tcs.SetResult(true);
        }

        #endregion

        #region Appending Nodes

        /// <summary>
        /// Appends the doctype token to the document.
        /// </summary>
        /// <param name="doctypeToken">The doctypen token.</param>
        void AddDoctype(HtmlDoctypeToken doctypeToken)
        {
            var node = new DocumentType();
            node.SystemId = doctypeToken.SystemIdentifier;
            node.PublicId = doctypeToken.PublicIdentifier;
            node.Name = doctypeToken.Name;
            doc.AppendChild(node);
        }

        /// <summary>
        /// Adds the root element (html) to the document.
        /// </summary>
        /// <param name="token">The token which started this process.</param>
        void AddRoot(HtmlToken token)
        {
            var element = new HTMLHtmlElement();
            doc.AppendChild(element);
            SetupElement(element, token, false);
            open.Add(element);
            tokenizer.AcceptsCharacterData = !element.IsInHtml;
        }

        /// <summary>
        /// Appends a comment node to the specified node.
        /// </summary>
        /// <param name="parent">The node which will contain the comment node.</param>
        /// <param name="commentToken">The comment token.</param>
        void AddComment(Node parent, HtmlToken commentToken)
        {
            var tag = (HtmlCommentToken)commentToken;
            var comment = new Comment();
            comment.Data = tag.Data;
            parent.AppendChild(comment);
        }

        /// <summary>
        /// Pops the last node from the stack of open nodes.
        /// </summary>
        void CloseCurrentNode()
        {
            if (open.Count > 0)
            {
                open.RemoveAt(open.Count - 1);
                var node = AdjustedCurrentNode;
                tokenizer.AcceptsCharacterData = node != null && !node.IsInHtml;
            }
        }

        /// <summary>
        /// Appends a node to the current node and
        /// modifies the node by appending all attributes and
        /// acknowledging the self-closing flag if set.
        /// </summary>
        /// <param name="element">The node which will be added to the list.</param>
        /// <param name="elementToken">The associated tag token.</param>
        /// <param name="acknowledgeSelfClosing">Should the self-closing be acknowledged?</param>
        void AddElementToCurrentNode(Element element, HtmlToken elementToken, Boolean acknowledgeSelfClosing = false)
        {
            SetupElement(element, elementToken, acknowledgeSelfClosing);
            AddElementToCurrentNode(element);
        }

        /// <summary>
        /// Appends a configured node to the current node.
        /// </summary>
        /// <param name="element">The node which will be added to the list.</param>
        void AddElementToCurrentNode(Element element)
        {
            if (foster && CurrentNode.IsTableElement())
                AddElementWithFoster(element);
            else
                CurrentNode.AppendChild(element);

            open.Add(element);
            tokenizer.AcceptsCharacterData = !element.IsInHtml;
        }

        /// <summary>
        /// Appends a node to the appropriate foster parent.
        /// </summary>
        /// <param name="element">The node which will be added to the list.</param>
        void AddElementWithFoster(Element element)
        {
            var table = false;
            var index = open.Count;

            while (--index != 0)
            {
                if (open[index] is HTMLTableElement)
                {
                    table = true;
                    break;
                }
            }

            var foster = open[index].ParentNode ?? open[index + 1];

            if (table && open[index].ParentNode != null)
            {
                for (int i = 0; i < foster.ChildNodes.Length; i++)
			    {
                    if (foster.ChildNodes[i] == open[index])
                    {
                        foster.InsertChild(i, element);
                        break;
                    }
			    }
            }
            else
                foster.AppendChild(element);
        }

        /// <summary>
        /// Modifies the node by appending all attributes and
        /// acknowledging the self-closing flag if set.
        /// </summary>
        /// <param name="element">The node which will be added to the list.</param>
        /// <param name="elementToken">The associated tag token.</param>
        /// <param name="acknowledgeSelfClosing">Should the self-closing be acknowledged?</param>
        void SetupElement(Element element, HtmlToken elementToken, bool acknowledgeSelfClosing)
        {
            var tag = (HtmlTagToken)elementToken;
            element.NodeName = tag.Name;

            if (tag.IsSelfClosing && !acknowledgeSelfClosing)
                RaiseErrorOccurred(ErrorCode.TagCannotBeSelfClosed);

            for (var i = 0; i < tag.Attributes.Count; i++)
                element.SetAttribute(tag.Attributes[i].Key, tag.Attributes[i].Value);
        }

        /// <summary>
        /// Checks for each attribute on the token if the attribute is already present on the node.
        /// If it is not, the attribute and its corresponding value is added to the node.
        /// </summary>
        /// <param name="elementToken">The token with the source attributes.</param>
        /// <param name="element">The node with the target attributes.</param>
        void AppendAttributes(HtmlTagToken elementToken, Element element)
        {
            foreach (var attr in elementToken.Attributes)
            {
                if (!element.HasAttribute(attr.Key))
                    element.SetAttribute(attr.Key, attr.Value);
            }
        }

        /// <summary>
        /// Inserts the given characters into the current node.
        /// </summary>
        /// <param name="p">The characters to insert.</param>
        void InsertCharacters(String p)
        {
            if (String.IsNullOrEmpty(p))
                return;
            else if (foster && CurrentNode.IsTableElement())
                InsertCharactersWithFoster(p);
            else
                CurrentNode.AppendText(p);
        }

        /// <summary>
        /// Inserts the given character into the foster parent.
        /// </summary>
        /// <param name="p">The character to insert.</param>
        void InsertCharactersWithFoster(String p)
        {
            var table = false;
            var index = open.Count;

            while (--index != 0)
            {
                if (open[index].NodeName == HTMLTableElement.Tag)
                {
                    table = true;
                    break;
                }
            }

            var foster = open[index].ParentNode ?? open[index + 1];

            if (table && open[index].ParentNode != null)
            {
                for (int i = 0; i < foster.ChildNodes.Length; i++)
                {
                    if (foster.ChildNodes[i] == open[index])
                    {
                        foster.InsertText(i, p);
                        break;
                    }
                }
            }
            else
                foster.AppendText(p);
        }

        /// <summary>
        /// Inserts a scope marker at the end of the list of active formatting elements.
        /// </summary>
        void InsertScopeMarker()
        {
            formatting.Add(ScopeMarkerNode.Element);
        }

        #endregion

        #region Closing Nodes

        /// <summary>
        /// Closes the currently open cell (td or th).
        /// </summary>
        void CloseTheCell()
        {
            if (IsInTableScope(HTMLTableCellElement.NormalTag))
                InCellEndTagCell(HTMLTableCellElement.NormalTag);
            else
                InCellEndTagCell(HTMLTableCellElement.HeadTag);
        }

        /// <summary>
        /// Clears the stack of open elements back to the given element name.
        /// </summary>
        /// <param name="tagName">The tag that will be the CurrentNode.</param>
        void ClearStackBackTo(string tagName)
        {
            while (CurrentNode.NodeName != tagName && !(CurrentNode is HTMLHtmlElement))
                CloseCurrentNode();
        }

        /// <summary>
        /// Clears the stack of open elements back to any heading element.
        /// </summary>
        void ClearStackBackToHeading()
        {
            while (!(CurrentNode is HTMLHeadingElement) && !(CurrentNode is HTMLHtmlElement))
                CloseCurrentNode();
        }

        /// <summary>
        /// Clears the stack of open elements back to any table section element.
        /// </summary>
        void ClearStackBackToTableSection()
        {
            while (!(CurrentNode is HTMLTableSectionElement) && !(CurrentNode is HTMLHtmlElement))
                CloseCurrentNode();
        }

        /// <summary>
        /// Clears the stack of open elements back to a table element.
        /// </summary>
        void ClearStackBackToTable()
        {
            while (!(CurrentNode is HTMLTableElement) && !(CurrentNode is HTMLHtmlElement))
                CloseCurrentNode();
        }

        /// <summary>
        /// Clears the stack of open elements back to a tr element.
        /// </summary>
        void ClearStackBackToTableRow()
        {
            while (!(CurrentNode is HTMLTableRowElement) && !(CurrentNode is HTMLHtmlElement))
                CloseCurrentNode();
        }

        /// <summary>
        /// Generates the implied end tags for the dd, dt, li, option, optgroup, p, rp, rt elements except for
        /// the tag given.
        /// </summary>
        /// <param name="tagName">The tag that will be excluded.</param>
        void GenerateImpliedEndTagsExceptFor(string tagName)
        {
            var list = new List<string>(new[] { HTMLLIElement.DefinitionTag, HTMLLIElement.DescriptionTag, 
                HTMLLIElement.ItemTag, HTMLOptGroupElement.Tag, HTMLOptionElement.Tag, HTMLParagraphElement.Tag, "rp", "rt" });

            if (list.Contains(tagName))
                list.Remove(tagName);

            while(list.Contains(CurrentNode.NodeName))
                CloseCurrentNode();
        }

        /// <summary>
        /// Generates the implied end tags for the dd, dt, li, option, optgroup, p, rp, rt elements.
        /// </summary>
        void GenerateImpliedEndTags()
        {
            while (CurrentNode is HTMLLIElement || CurrentNode is HTMLOptionElement || CurrentNode is HTMLOptGroupElement || 
                CurrentNode is HTMLParagraphElement || CurrentNode.NodeName == "rp" || CurrentNode.NodeName == "rt")
                CloseCurrentNode();
        }

        #endregion

        #region Formatting

        /// <summary>
        /// Adds an element to the list of active formatting elements.
        /// </summary>
        /// <param name="element">The element to add.</param>
        void AddFormattingElement(Element element)
        {
            var count = 0;

            for (var i = formatting.Count - 1; i >= 0; i--)
            {
                if (formatting[i] is ScopeMarkerNode)
                    break;

                if (formatting[i].NodeName == element.NodeName && formatting[i].Attributes.Equals(element.Attributes) && formatting[i].NamespaceURI == element.NamespaceURI)
                {
                    count++;

                    if (count == 3)
                    {
                        formatting.RemoveAt(i);
                        break;
                    }
                }
            }

            formatting.Add(element);
        }

        /// <summary>
        /// Clear the list of active formatting elements up to the last marker.
        /// </summary>
        void ClearFormattingElements()
        {
            while (formatting.Count != 0)
            {
                var entry = formatting[formatting.Count - 1];
                formatting.Remove(entry);

                if (entry is ScopeMarkerNode)
                    break;
            }
        }

        /// <summary>
        /// Reconstruct the list of active formatting elements, if any.
        /// </summary>
        void ReconstructFormatting()
        {
            if (formatting.Count == 0)
                return;

            var index = formatting.Count - 1;
            var entry = formatting[index];

            if (entry is ScopeMarkerNode || open.Contains(entry))
                return;

            while (index > 0)
            {
                entry = formatting[--index];

                if (entry is ScopeMarkerNode || open.Contains(entry))
                {
                    index++;
                    break;
                }
            }

            for (; index < formatting.Count; index++)
            {
                var element = CopyElement(formatting[index]);
                AddElementToCurrentNode(element);

                formatting[index] = element;
            }
        }

        #endregion

        #region Handlers

        /// <summary>
        /// Fires an error occurred event.
        /// </summary>
        /// <param name="code">The associated error code.</param>
        void RaiseErrorOccurred(ErrorCode code)
        {
            if (ErrorOccurred != null)
            {
                var pck = new ParseErrorEventArgs((int)code, Errors.GetError(code));
                pck.Line = tokenizer.Stream.Line;
                pck.Column = tokenizer.Stream.Column;
                ErrorOccurred(this, pck);
            }
        }

        #endregion
    }
}
