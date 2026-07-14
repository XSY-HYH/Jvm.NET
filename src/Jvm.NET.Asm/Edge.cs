// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// An edge in the control flow graph of a method. Each node of this graph is a basic block,
/// represented with the Label corresponding to its first instruction. Each edge goes from one node
/// to another, i.e. from one basic block to another (called the predecessor and successor blocks,
/// respectively). An edge corresponds either to a jump or ret instruction or to an exception
/// handler.
/// </summary>
/// <seealso cref="Label"/>
internal sealed class Edge
{
    /// <summary>
    /// A control flow graph edge corresponding to a jump or ret instruction. Only used with
    /// ClassWriter.COMPUTE_FRAMES.
    /// </summary>
    internal const int JUMP = 0;

    /// <summary>
    /// A control flow graph edge corresponding to an exception handler. Only used with
    /// ClassWriter.COMPUTE_MAXS.
    /// </summary>
    internal const int EXCEPTION = 0x7FFFFFFF;

    /// <summary>
    /// Information about this control flow graph edge.
    /// <para>
    /// If ClassWriter.COMPUTE_MAXS is used, this field contains either a stack size
    /// delta (for an edge corresponding to a jump instruction), or the value EXCEPTION (for an
    /// edge corresponding to an exception handler). The stack size delta is the stack size just
    /// after the jump instruction, minus the stack size at the beginning of the predecessor
    /// basic block, i.e. the one containing the jump instruction.
    /// </para>
    /// <para>
    /// If ClassWriter.COMPUTE_FRAMES is used, this field contains either the value JUMP
    /// (for an edge corresponding to a jump instruction), or the index, in the ClassWriter
    /// type table, of the exception type that is handled (for an edge corresponding to an
    /// exception handler).
    /// </para>
    /// </summary>
    internal readonly int Info;

    /// <summary>The successor block of this control flow graph edge.</summary>
    internal readonly Label Successor;

    /// <summary>
    /// The next edge in the list of outgoing edges of a basic block. See <see cref="Label.OutgoingEdges"/>.
    /// </summary>
    internal Edge? NextEdge;

    /// <summary>
    /// Constructs a new Edge.
    /// </summary>
    /// <param name="info">see <see cref="Info"/>.</param>
    /// <param name="successor">see <see cref="Successor"/>.</param>
    /// <param name="nextEdge">see <see cref="NextEdge"/>.</param>
    internal Edge(int info, Label successor, Edge? nextEdge)
    {
        Info = info;
        Successor = successor;
        NextEdge = nextEdge;
    }
}
