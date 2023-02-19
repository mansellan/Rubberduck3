﻿using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Rubberduck.Parsing.Model;
using System.Threading;

namespace Rubberduck.Parsing.Abstract
{
    public interface ITokenStreamParser
    {
        IParseTree Parse(string moduleName, string projectId, CommonTokenStream tokenStream, CancellationToken token, CodeKind codeKind = CodeKind.RubberduckEditorModule, ParserMode parserMode = ParserMode.FallBackSllToLl);
    }
}
