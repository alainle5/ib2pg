namespace IB2PG;

public sealed class GeneratorException : Exception
{
    public AstNode? Node { get; }

    public GeneratorException(string message, AstNode? node = null)
        : base(message)
    {
        Node = node;
    }
}
