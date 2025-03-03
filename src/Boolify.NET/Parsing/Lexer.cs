﻿using Boolify.NET.Tokens;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Boolify.NET.Parsing;

internal ref struct Lexer(string expression)
{
    private readonly string _inputExpression = expression;
    private ReadOnlySpan<char> _workingSpan = expression.AsSpan().TrimStart();
    private CurrentToken? _current;
    private readonly int Index => _inputExpression.Length - _workingSpan.Length;

    public readonly IToken? Current => _current?.Token;
    public readonly int CurrentIndex => _current?.Index ?? -1;

    public bool Advance()
        => TryGetNextToken(out _current);

    private bool TryGetNextToken([NotNullWhen(true)] out CurrentToken? token)
    {
        if (_workingSpan.IsEmpty)
        {
            token = null;
            return false;
        }

        if (char.IsWhiteSpace(_workingSpan[0]))
        {
            _workingSpan = _workingSpan.TrimStart();
        }

        if (TryCreateTokenAndAdvance(out token))
        {
            return true;
        }

        // If it's not white space, and can't parse a bool or operand,
        // then it's an unknown token.
        throw new Exception(/*TODO: Replace with custom exception*/$"Unsupported format: unrecognised token at index {Index}; \"{_inputExpression.Insert(Index, "*")}\"");
    }

    private bool TryCreateTokenAndAdvance([NotNullWhen(true)] out CurrentToken? token)
    {
        var firstChar = char.ToLowerInvariant(_workingSpan[0]);

        var lookup = _tokenFirstCharToValue.GetValueOrDefault(firstChar);

        foreach (var value in lookup)
        {
            var valueSpan = value.AsSpan();
            if (_workingSpan.StartsWith(valueSpan, StringComparison.OrdinalIgnoreCase) && _tokenValueToType.TryGetValue(value, out var tokenType))
            {
                var tempToken = tokenType.ToToken();

                ValidateToken(tempToken);

                token = new(tempToken, Index, value.Length);

                _workingSpan = _workingSpan.Slice(value.Length).TrimStart();

                return true;
            }
        }

        token = null;
        return false;
    }

    private readonly void ValidateToken(IToken token)
    {
        if (Current is BoolToken && token is BoolToken)
        {
            throw new Exception(/*TODO: Replace with custom exception*/$"Unsupported format: unexpected token at index {Index}; \"{_inputExpression.Insert(Index, "*")}\"");
        }

        if (Current is IOperandToken token1 && token is IOperandToken token2)
        {
            ValidateConsecutiveOperandTokens(token1, token2);
        }
    }

    private readonly void ValidateConsecutiveOperandTokens(IOperandToken token1, IOperandToken token2)
    {
        switch ((token1, token2))
        {
            // (!, && !, || !, ^ !, !!, and !, or !, xor !
            case (OpenParenthesisToken or AndToken or OrToken or XorToken or NotToken, NotToken):
            // !(
            case (NotToken, OpenParenthesisToken):
                // Valid
                return;
        }
        throw new Exception(/*TODO: Replace with custom exception*/$"Unsupported format: unexpected token at index {Index}; \"{_inputExpression.Insert(Index, "*")}\"");
    }

    private readonly record struct CurrentToken(IToken Token, int Index, int Length);

    // TODO: These could be configurable by the library user through a builder.
    // This could be created in the constructor from the valid tokens
    // Take the lower invariant of the first char of each token
    private static readonly ImmutableDictionary<char, ImmutableArray<string>> _tokenFirstCharToValue = new Dictionary<char, ImmutableArray<string>>
    {
        ['t'] = ["true"],
        ['f'] = ["false"],
        ['a'] = ["and"],
        ['&'] = ["&&"],
        ['o'] = ["or"],
        ['|'] = ["||"],
        ['n'] = ["not"],
        ['!'] = ["!"],
        ['x'] = ["xor"],
        ['^'] = ["^"],
        ['('] = ["("],
        [')'] = [")"]
    }.ToImmutableDictionary();

    // TODO: Hardcoded for now
    private static readonly ImmutableDictionary<string, TokenType> _tokenValueToType = new Dictionary<string, TokenType>
    {
        ["true"] = TokenType.BoolTrue,
        ["false"] = TokenType.BoolFalse,
        ["and"] = TokenType.And,
        ["&&"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["||"] = TokenType.Or,
        ["not"] = TokenType.Not,
        ["!"] = TokenType.Not,
        ["xor"] = TokenType.Xor,
        ["^"] = TokenType.Xor,
        ["("] = TokenType.OpenParenthesis,
        [")"] = TokenType.CloseParenthesis
    }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
}
