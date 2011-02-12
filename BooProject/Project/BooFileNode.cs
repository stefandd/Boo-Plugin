﻿//
//   Copyright © 2010 Michael Feingold
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Boo.Lang.Compiler;
using Boo.Lang.Compiler.TypeSystem;
using Hill30.BooProject.AST;
using Hill30.BooProject.AST.Nodes;
using Hill30.BooProject.Compilation;
using Microsoft.VisualStudio.Project;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell;
using Hill30.BooProject.LanguageService;

namespace Hill30.BooProject.Project
{
    
    public class BooFileNode : FileNode, IFileNode
    {
        private CompileResults results;
        private ITextBuffer textBuffer;
        private ITextSnapshot originalSnapshot;
        private bool hidden;
        private BooLanguageService languageService;

        private CompileResults GetResults()
        {
            return results;
        }

        public BooFileNode(ProjectNode root, ProjectElement e)
			: base(root, e)
		{
            results = new CompileResults();
            languageService = (BooLanguageService)GetService(typeof(BooLanguageService));
            hidden = true;
        }

        public void Bind(ITextBuffer buffer)
        {
            textBuffer = buffer;
            if (buffer == null)
                hidden = true;
            else
                originalSnapshot = buffer.CurrentSnapshot;
        }

        public ICompilerInput GetCompilerInput(CompileResults results)
        {
            if (textBuffer == null)
                return results.Initialize(Url, File.ReadAllText(Url), GlobalServices.LanguageService.GetLanguagePreferences().TabSize);
            hidden = false;
            originalSnapshot = textBuffer.CurrentSnapshot;
            return results.Initialize(Url, originalSnapshot.GetText(), GlobalServices.LanguageService.GetLanguagePreferences().TabSize);
        }

        public void SetCompilerResults(CompileResults newResults)
        {
            results.HideMessages(((BooProjectNode)ProjectMgr).RemoveTask);
            results = newResults;
            if (!hidden)
                results.ShowMessages(((BooProjectNode)ProjectMgr).AddTask, Navigate);
            if (Recompiled != null)
                Recompiled(this, EventArgs.Empty);
        }

        private void Navigate(ErrorTask target)
        {
            ProjectMgr.Navigate(target.Document, target.Line, target.Column);
        }

        /// <summary>
        /// Return an imageindex
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        public override int ImageIndex
        {
            get
            {
                if (Path.GetExtension(FileName) == ".boo")
                    return BooProjectNode.ImageOffset + (int)BooProjectNode.ProjectIcons.File;
                return base.ImageIndex;
            }
        }

        #region Private implementation
   
        internal OleServiceProvider.ServiceCreatorCallback ServiceCreator
        {
            get { return CreateServices; }
        }

        private object CreateServices(Type serviceType)
        {
            object service = null;
            if (typeof(EnvDTE.ProjectItem) == serviceType)
            {
                service = GetAutomationObject();
            }
            return service;
        }
        
        #endregion

        public ICompileUnit CompileUnit { get { return GetResults().CompileUnit; } }

        private SnapshotSpan SnapshotCreator(TextSpan textspan)
        {
            if (textBuffer == null)
                return default(SnapshotSpan);

            var startIndex = originalSnapshot.GetLineFromLineNumber(textspan.iStartLine).Start + textspan.iStartIndex;
            var endLine = originalSnapshot.GetLineFromLineNumber(textspan.iEndLine);
            return 
                textspan.iEndIndex == -1 
                ? new SnapshotSpan(originalSnapshot, startIndex, endLine.Start + endLine.Length - startIndex) 
                : new SnapshotSpan(originalSnapshot, startIndex, endLine.Start + textspan.iEndIndex - startIndex);
        }
        
        #region IFileNode Members

        public event EventHandler Recompiled;

        public MappedToken GetMappedToken(int line, int col) { return GetResults().GetMappedToken(line, col); }

        public MappedToken GetAdjacentMappedToken(int line, int col) { return GetResults().GetAdjacentMappedToken(line, col); }

        public IEnumerable<MappedTypeDefinition> Types { get { return GetResults().Types; } }

        public CompileResults.BufferPoint MapPosition(int line, int column) { return GetResults().LocationToPoint(line, column); }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) 
        {
            return GetResults().GetClassificationSpans(languageService.ClassificationTypeRegistry, span, SnapshotCreator); 
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) { return GetResults().GetTags(spans, SnapshotCreator); }

        public void HideMessages() { GetResults().HideMessages(((BooProjectNode)ProjectMgr).RemoveTask); }

        public void ShowMessages() { GetResults().ShowMessages(((BooProjectNode)ProjectMgr).AddTask, Navigate); }

        public void SubmitForCompile() { ((BooProjectNode) ProjectMgr).SubmitForCompile(this); }

        #endregion

    }
}
