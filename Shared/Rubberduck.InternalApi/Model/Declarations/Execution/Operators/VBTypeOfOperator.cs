﻿using Rubberduck.InternalApi.Extensions;
using Rubberduck.InternalApi.Model.Declarations.Execution;
using Rubberduck.InternalApi.Model.Declarations.Execution.Values;
using Rubberduck.InternalApi.Model.Declarations.Operators.Abstract;
using Rubberduck.InternalApi.Model.Declarations.Symbols;
using System;
using System.Linq;

namespace Rubberduck.InternalApi.Model.Declarations.Operators;

public record class VBTypeOfOperator : VBUnaryOperator
{
    public VBTypeOfOperator(string expression, TypedSymbol operand, WorkspaceUri parentUri)
        : base(Tokens.TypeOf, expression, parentUri, operand)
    {
    }

    protected override VBTypedValue? EvaluateResult(ref VBExecutionScope context) =>
        new VBTypeDescValue((TypedSymbol)Children!.Single());
}
