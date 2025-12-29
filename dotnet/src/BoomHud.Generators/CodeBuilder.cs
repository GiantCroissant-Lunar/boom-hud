using System.Text;

namespace BoomHud.Generators;

/// <summary>
/// Utility for building indented source code.
/// </summary>
public sealed class CodeBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel;
    private readonly string _indentString;

    public CodeBuilder(string indentString = "    ")
    {
        _indentString = indentString;
    }

    /// <summary>
    /// Appends a line with current indentation.
    /// </summary>
    public CodeBuilder AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(GetIndent());
            _sb.AppendLine(line);
        }
        return this;
    }

    /// <summary>
    /// Appends text without a newline.
    /// </summary>
    public CodeBuilder Append(string text)
    {
        _sb.Append(text);
        return this;
    }

    /// <summary>
    /// Increases indentation level.
    /// </summary>
    public CodeBuilder Indent()
    {
        _indentLevel++;
        return this;
    }

    /// <summary>
    /// Decreases indentation level.
    /// </summary>
    public CodeBuilder Outdent()
    {
        if (_indentLevel > 0)
            _indentLevel--;
        return this;
    }

    /// <summary>
    /// Opens a block with brace and increases indent.
    /// </summary>
    public CodeBuilder OpenBlock(string? prefix = null)
    {
        if (!string.IsNullOrEmpty(prefix))
            AppendLine(prefix);
        AppendLine("{");
        Indent();
        return this;
    }

    /// <summary>
    /// Closes a block with brace and decreases indent.
    /// </summary>
    public CodeBuilder CloseBlock(string? suffix = null)
    {
        Outdent();
        AppendLine("}" + (suffix ?? ""));
        return this;
    }

    /// <summary>
    /// Creates an indented scope that auto-outdents on dispose.
    /// </summary>
    public IDisposable IndentScope() => new IndentScopeDisposable(this);

    /// <summary>
    /// Creates a block scope with braces.
    /// </summary>
    public IDisposable BlockScope(string? prefix = null)
    {
        OpenBlock(prefix);
        return new BlockScopeDisposable(this);
    }

    private string GetIndent() => string.Concat(Enumerable.Repeat(_indentString, _indentLevel));

    public override string ToString() => _sb.ToString();

    private sealed class IndentScopeDisposable(CodeBuilder builder) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                builder.Outdent();
                _disposed = true;
            }
        }
    }

    private sealed class BlockScopeDisposable(CodeBuilder builder) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                builder.CloseBlock();
                _disposed = true;
            }
        }
    }
}
