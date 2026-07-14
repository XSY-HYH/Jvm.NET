// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// A position in the bytecode of a method. Labels are used for jump, goto, and switch instructions,
/// and for try catch blocks. A label designates the <i>instruction</i> that is just after. Note
/// however that there can be other elements between a label and the instruction it designates (such
/// as other labels, stack map frames, line numbers, etc.).
/// </summary>
public class Label
{
    // -----------------------------------------------------------------------------------------------
    // Constants that should belong to the Constants class.
    // TODO: move to Constants when it is ported.

    /// <summary>
    /// The delta between ASM specific opcodes and standard opcodes for IFEQ..JSR.
    /// </summary>
    internal const int ASM_OPCODE_DELTA = 49;

    /// <summary>
    /// The delta between ASM specific opcodes and standard opcodes for IFNULL/IFNONNULL.
    /// </summary>
    internal const int ASM_IFNULL_OPCODE_DELTA = 20;

    // -----------------------------------------------------------------------------------------------
    // Flag constants

    /// <summary>
    /// A flag indicating that a label is only used for debug attributes. Such a label is not the start
    /// of a basic block, the target of a jump instruction, or an exception handler. It can be safely
    /// ignored in control flow graph analysis algorithms (for optimization purposes).
    /// </summary>
    internal const int FLAG_DEBUG_ONLY = 1;

    /// <summary>
    /// A flag indicating that a label is the target of a jump instruction, or the start of an
    /// exception handler.
    /// </summary>
    internal const int FLAG_JUMP_TARGET = 2;

    /// <summary>A flag indicating that the bytecode offset of a label is known.</summary>
    internal const int FLAG_RESOLVED = 4;

    /// <summary>A flag indicating that a label corresponds to a reachable basic block.</summary>
    internal const int FLAG_REACHABLE = 8;

    /// <summary>
    /// A flag indicating that the basic block corresponding to a label ends with a subroutine call. By
    /// construction in MethodWriter.visitJumpInsn, labels with this flag set have at least two
    /// outgoing edges:
    /// <para>
    /// - the first one corresponds to the instruction that follows the jsr instruction in the
    ///   bytecode, i.e. where execution continues when it returns from the jsr call. This is a
    ///   virtual control flow edge, since execution never goes directly from the jsr to the next
    ///   instruction. Instead, it goes to the subroutine and eventually returns to the instruction
    ///   following the jsr. This virtual edge is used to compute the real outgoing edges of the
    ///   basic blocks ending with a ret instruction, in AddSubroutineRetSuccessors.
    /// </para>
    /// <para>
    /// - the second one corresponds to the target of the jsr instruction.
    /// </para>
    /// </summary>
    internal const int FLAG_SUBROUTINE_CALLER = 16;

    /// <summary>
    /// A flag indicating that the basic block corresponding to a label is the start of a subroutine.
    /// </summary>
    internal const int FLAG_SUBROUTINE_START = 32;

    /// <summary>A flag indicating that the basic block corresponding to a label is the end of a subroutine.</summary>
    internal const int FLAG_SUBROUTINE_END = 64;

    /// <summary>A flag indicating that this label has at least one associated line number.</summary>
    internal const int FLAG_LINE_NUMBER = 128;

    // -----------------------------------------------------------------------------------------------
    // Capacity increments

    /// <summary>
    /// The number of elements to add to the <see cref="_otherLineNumbers"/> array when it needs to be
    /// resized to store a new source line number.
    /// </summary>
    internal const int LINE_NUMBERS_CAPACITY_INCREMENT = 4;

    /// <summary>
    /// The number of elements to add to the <see cref="_forwardReferences"/> array when it needs to be
    /// resized to store a new forward reference.
    /// </summary>
    internal const int FORWARD_REFERENCES_CAPACITY_INCREMENT = 6;

    // -----------------------------------------------------------------------------------------------
    // Forward reference types

    /// <summary>
    /// The bit mask to extract the type of a forward reference to this label. The extracted type is
    /// either <see cref="FORWARD_REFERENCE_TYPE_SHORT"/> or <see cref="FORWARD_REFERENCE_TYPE_WIDE"/>.
    /// </summary>
    internal const int FORWARD_REFERENCE_TYPE_MASK = unchecked((int)0xF0000000);

    /// <summary>
    /// The type of forward references stored with two bytes in the bytecode. This is the case, for
    /// instance, of a forward reference from an ifnull instruction.
    /// </summary>
    internal const int FORWARD_REFERENCE_TYPE_SHORT = 0x10000000;

    /// <summary>
    /// The type of forward references stored in four bytes in the bytecode. This is the case, for
    /// instance, of a forward reference from a lookupswitch instruction.
    /// </summary>
    internal const int FORWARD_REFERENCE_TYPE_WIDE = 0x20000000;

    /// <summary>
    /// The type of forward references stored in two bytes in the <i>stack map table</i>. This is the
    /// case of the labels of Frame.ITEM_UNINITIALIZED stack map frame elements, when the NEW
    /// instruction is after the &lt;init&gt; constructor call (in bytecode offset order).
    /// </summary>
    internal const int FORWARD_REFERENCE_TYPE_STACK_MAP = 0x30000000;

    /// <summary>
    /// The bit mask to extract the 'handle' of a forward reference to this label. The extracted handle
    /// is the bytecode offset where the forward reference value is stored (using either 2 or 4 bytes,
    /// as indicated by the <see cref="FORWARD_REFERENCE_TYPE_MASK"/>).
    /// </summary>
    internal const int FORWARD_REFERENCE_HANDLE_MASK = 0x0FFFFFFF;

    /// <summary>
    /// A sentinel element used to indicate the end of a list of labels.
    /// </summary>
    internal static readonly Label EMPTY_LIST = new Label();

    // -----------------------------------------------------------------------------------------------
    // Fields

    /// <summary>
    /// A user managed state associated with this label. Warning: this field is used by the ASM tree
    /// package. In order to use it with the ASM tree package you must override the getLabelNode method
    /// in MethodNode.
    /// </summary>
    public object? Info;

    /// <summary>
    /// The type and status of this label or its corresponding basic block. Must be zero or more of
    /// <see cref="FLAG_DEBUG_ONLY"/>, <see cref="FLAG_JUMP_TARGET"/>, <see cref="FLAG_RESOLVED"/>,
    /// <see cref="FLAG_REACHABLE"/>, <see cref="FLAG_SUBROUTINE_CALLER"/>,
    /// <see cref="FLAG_SUBROUTINE_START"/>, <see cref="FLAG_SUBROUTINE_END"/>.
    /// </summary>
    internal short Flags;

    /// <summary>
    /// The source line number corresponding to this label, if <see cref="FLAG_LINE_NUMBER"/> is set. If
    /// there are several source line numbers corresponding to this label, the first one is stored in
    /// this field, and the remaining ones are stored in <see cref="_otherLineNumbers"/>.
    /// </summary>
    private short _lineNumber;

    /// <summary>
    /// The source line numbers corresponding to this label, in addition to <see cref="_lineNumber"/>, or
    /// null. The first element of this array is the number n of source line numbers it contains, which
    /// are stored between indices 1 and n (inclusive).
    /// </summary>
    private int[]? _otherLineNumbers;

    /// <summary>
    /// The offset of this label in the bytecode of its method, in bytes. This value is set if and only
    /// if the <see cref="FLAG_RESOLVED"/> flag is set.
    /// </summary>
    internal int BytecodeOffset;

    /// <summary>
    /// The forward references to this label. The first element is the number of forward references,
    /// times 2 (this corresponds to the index of the last element actually used in this array). Then,
    /// each forward reference is described with two consecutive integers noted
    /// 'sourceInsnBytecodeOffset' and 'reference':
    /// <para>
    /// - 'sourceInsnBytecodeOffset' is the bytecode offset of the instruction that contains the
    ///   forward reference,
    /// </para>
    /// <para>
    /// - 'reference' contains the type and the offset in the bytecode where the forward reference
    ///   value must be stored, which can be extracted with <see cref="FORWARD_REFERENCE_TYPE_MASK"/>
    ///   and <see cref="FORWARD_REFERENCE_HANDLE_MASK"/>.
    /// </para>
    /// <para>
    /// For instance, for an ifnull instruction at bytecode offset x, 'sourceInsnBytecodeOffset' is
    /// equal to x, and 'reference' is of type <see cref="FORWARD_REFERENCE_TYPE_SHORT"/> with value x + 1
    /// (because the ifnull instruction uses a 2 bytes bytecode offset operand stored one byte after
    /// the start of the instruction itself). For the default case of a lookupswitch instruction at
    /// bytecode offset x, 'sourceInsnBytecodeOffset' is equal to x, and 'reference' is of type
    /// <see cref="FORWARD_REFERENCE_TYPE_WIDE"/> with value between x + 1 and x + 4 (because the lookupswitch
    /// instruction uses a 4 bytes bytecode offset operand stored one to four bytes after the start of
    /// the instruction itself).
    /// </para>
    /// </summary>
    private int[]? _forwardReferences;

    // -----------------------------------------------------------------------------------------------
    // Fields for the control flow and data flow graph analysis algorithms (used to compute the
    // maximum stack size or the stack map frames). A control flow graph contains one node per "basic
    // block", and one edge per "jump" from one basic block to another. Each node (i.e., each basic
    // block) is represented with the Label object that corresponds to the first instruction of this
    // basic block. Each node also stores the list of its successors in the graph, as a linked list of
    // Edge objects.
    //
    // The control flow analysis algorithms used to compute the maximum stack size or the stack map
    // frames are similar and use two steps. The first step, during the visit of each instruction,
    // builds information about the state of the local variables and the operand stack at the end of
    // each basic block, called the "output frame", relatively to the frame state at the beginning of
    // the basic block, which is called the "input frame", and which is unknown during this step. The
    // second step, in MethodWriter.computeAllFrames and MethodWriter.computeMaxStackAndLocal, is a
    // fix point algorithm that computes information about the input frame of each basic block, from
    // the input state of the first basic block (known from the method signature), and by the using
    // the previously computed relative output frames.
    //
    // The algorithm used to compute the maximum stack size only computes the relative output and
    // absolute input stack heights, while the algorithm used to compute stack map frames computes
    // relative output frames and absolute input frames.

    /// <summary>
    /// The number of elements in the input stack of the basic block corresponding to this label. This
    /// field is computed in MethodWriter.computeMaxStackAndLocal.
    /// </summary>
    internal short InputStackSize;

    /// <summary>
    /// The number of elements in the output stack, at the end of the basic block corresponding to this
    /// label. This field is only computed for basic blocks that end with a RET instruction.
    /// </summary>
    internal short OutputStackSize;

    /// <summary>
    /// The maximum height reached by the output stack, relatively to the top of the input stack, in
    /// the basic block corresponding to this label. This maximum is always positive or null.
    /// </summary>
    internal short OutputStackMax;

    /// <summary>
    /// The id of the subroutine to which this basic block belongs, or 0. If the basic block belongs to
    /// several subroutines, this is the id of the "oldest" subroutine that contains it (with the
    /// convention that a subroutine calling another one is "older" than the callee). This field is
    /// computed in MethodWriter.computeMaxStackAndLocal, if the method contains JSR
    /// instructions.
    /// </summary>
    internal short SubroutineId;

    /// <summary>
    /// The input and output stack map frames of the basic block corresponding to this label. This
    /// field is only used when the MethodWriter.COMPUTE_ALL_FRAMES or
    /// MethodWriter.COMPUTE_INSERTED_FRAMES option is used.
    /// </summary>
    internal Frame? frame;

    /// <summary>
    /// The successor of this label, in the order they are visited in MethodVisitor.visitLabel.
    /// This linked list does not include labels used for debug info only. If the
    /// MethodWriter.COMPUTE_ALL_FRAMES or MethodWriter.COMPUTE_INSERTED_FRAMES option is used
    /// then it does not contain either successive labels that denote the same bytecode offset (in this
    /// case only the first label appears in this list).
    /// </summary>
    internal Label? NextBasicBlock;

    /// <summary>
    /// The outgoing edges of the basic block corresponding to this label, in the control flow graph of
    /// its method. These edges are stored in a linked list of <see cref="Edge"/> objects, linked to each
    /// other by their <see cref="Edge.NextEdge"/> field.
    /// </summary>
    internal Edge? OutgoingEdges;

    /// <summary>
    /// The next element in the list of labels to which this label belongs, or null if it
    /// does not belong to any list. All lists of labels must end with the <see cref="EMPTY_LIST"/>
    /// sentinel, in order to ensure that this field is null if and only if this label does not belong
    /// to a list of labels. Note that there can be several lists of labels at the same time, but that
    /// a label can belong to at most one list at a time (unless some lists share a common tail, but
    /// this is not used in practice).
    /// <para>
    /// List of labels are used in MethodWriter.computeAllFrames and
    /// MethodWriter.computeMaxStackAndLocal to compute stack map frames and the maximum stack size,
    /// respectively, as well as in MarkSubroutine and AddSubroutineRetSuccessors to compute the
    /// basic blocks belonging to subroutines and their outgoing edges. Outside of these methods, this
    /// field should be null (this property is a precondition and a postcondition of these methods).
    /// </para>
    /// </summary>
    internal Label? NextListElement;

    // -----------------------------------------------------------------------------------------------
    // Constructor and accessors
    // -----------------------------------------------------------------------------------------------

    /// <summary>Constructs a new label.</summary>
    public Label()
    {
        // Nothing to do.
    }

    /// <summary>
    /// Returns the bytecode offset corresponding to this label. This offset is computed from the start
    /// of the method's bytecode. <i>This method is intended for Attribute sub classes, and is
    /// normally not needed by class generators or adapters.</i>
    /// </summary>
    /// <returns>the bytecode offset corresponding to this label.</returns>
    /// <exception cref="InvalidOperationException">if this label is not resolved yet.</exception>
    public int GetOffset()
    {
        if ((Flags & FLAG_RESOLVED) == 0)
        {
            throw new InvalidOperationException("Label offset position has not been resolved yet");
        }
        return BytecodeOffset;
    }

    /// <summary>
    /// Returns the "canonical" <see cref="Label"/> instance corresponding to this label's bytecode offset,
    /// if known, otherwise the label itself. The canonical instance is the first label (in the order
    /// of their visit by MethodVisitor.visitLabel) corresponding to this bytecode offset. It
    /// cannot be known for labels which have not been visited yet.
    /// <para>
    /// <i>This method should only be used when the MethodWriter.COMPUTE_ALL_FRAMES option
    /// is used.</i>
    /// </para>
    /// </summary>
    /// <returns>
    /// the label itself if Frame is null, otherwise the Label's frame owner. This
    /// corresponds to the "canonical" label instance described above thanks to the way the label
    /// frame is set in MethodWriter.visitLabel.
    /// </returns>
    internal Label GetCanonicalInstance()
    {
        return frame == null ? this : frame.owner;
    }

    // -----------------------------------------------------------------------------------------------
    // Methods to manage line numbers
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a source line number corresponding to this label.
    /// </summary>
    /// <param name="lineNumber">a source line number (which should be strictly positive).</param>
    internal void AddLineNumber(int lineNumber)
    {
        if ((Flags & FLAG_LINE_NUMBER) == 0)
        {
            Flags |= FLAG_LINE_NUMBER;
            _lineNumber = (short)lineNumber;
        }
        else
        {
            if (_otherLineNumbers == null)
            {
                _otherLineNumbers = new int[LINE_NUMBERS_CAPACITY_INCREMENT];
            }
            int[] otherLineNumbers = _otherLineNumbers;
            int otherLineNumberIndex = ++otherLineNumbers[0];
            if (otherLineNumberIndex >= otherLineNumbers.Length)
            {
                int[] newLineNumbers = new int[otherLineNumbers.Length + LINE_NUMBERS_CAPACITY_INCREMENT];
                Array.Copy(otherLineNumbers, 0, newLineNumbers, 0, otherLineNumbers.Length);
                _otherLineNumbers = newLineNumbers;
                otherLineNumbers = newLineNumbers;
            }
            otherLineNumbers[otherLineNumberIndex] = lineNumber;
        }
    }

    /// <summary>
    /// Makes the given visitor visit this label and its source line numbers, if applicable.
    /// </summary>
    /// <param name="methodVisitor">a method visitor.</param>
    /// <param name="visitLineNumbers">whether to visit of the label's source line numbers, if any.</param>
    internal void Accept(MethodVisitor methodVisitor, bool visitLineNumbers)
    {
        methodVisitor.VisitLabel(this);
        if (visitLineNumbers && (Flags & FLAG_LINE_NUMBER) != 0)
        {
            methodVisitor.VisitLineNumber(_lineNumber & 0xFFFF, this);
            if (_otherLineNumbers != null)
            {
                for (int i = 1; i <= _otherLineNumbers[0]; ++i)
                {
                    methodVisitor.VisitLineNumber(_otherLineNumbers[i], this);
                }
            }
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Methods to compute offsets and to manage forward references
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Puts a reference to this label in the bytecode of a method. If the bytecode offset of the label
    /// is known, the relative bytecode offset between the label and the instruction referencing it is
    /// computed and written directly. Otherwise, a null relative offset is written and a new forward
    /// reference is declared for this label.
    /// </summary>
    /// <param name="code">the bytecode of the method. This is where the reference is appended.</param>
    /// <param name="sourceInsnBytecodeOffset">the bytecode offset of the instruction that contains the
    /// reference to be appended.</param>
    /// <param name="wideReference">whether the reference must be stored in 4 bytes (instead of 2 bytes).</param>
    internal void Put(ByteVector code, int sourceInsnBytecodeOffset, bool wideReference)
    {
        if ((Flags & FLAG_RESOLVED) == 0)
        {
            if (wideReference)
            {
                AddForwardReference(sourceInsnBytecodeOffset, FORWARD_REFERENCE_TYPE_WIDE, code.Length);
                code.PutInt(-1);
            }
            else
            {
                AddForwardReference(sourceInsnBytecodeOffset, FORWARD_REFERENCE_TYPE_SHORT, code.Length);
                code.PutShort(-1);
            }
        }
        else
        {
            if (wideReference)
            {
                code.PutInt(BytecodeOffset - sourceInsnBytecodeOffset);
            }
            else
            {
                code.PutShort(BytecodeOffset - sourceInsnBytecodeOffset);
            }
        }
    }

    /// <summary>
    /// Puts a reference to this label in the <i>stack map table</i> of a method. If the bytecode
    /// offset of the label is known, it is written directly. Otherwise, a null relative offset is
    /// written and a new forward reference is declared for this label.
    /// </summary>
    /// <param name="stackMapTableEntries">the stack map table where the label offset must be added.</param>
    internal void Put(ByteVector stackMapTableEntries)
    {
        if ((Flags & FLAG_RESOLVED) == 0)
        {
            AddForwardReference(0, FORWARD_REFERENCE_TYPE_STACK_MAP, stackMapTableEntries.Length);
        }
        stackMapTableEntries.PutShort(BytecodeOffset);
    }

    /// <summary>
    /// Adds a forward reference to this label. This method must be called only for a true forward
    /// reference, i.e. only if this label is not resolved yet. For backward references, the relative
    /// bytecode offset of the reference can be, and must be, computed and stored directly.
    /// </summary>
    /// <param name="sourceInsnBytecodeOffset">the bytecode offset of the instruction that contains the
    /// reference stored at referenceHandle.</param>
    /// <param name="referenceType">either <see cref="FORWARD_REFERENCE_TYPE_SHORT"/> or
    /// <see cref="FORWARD_REFERENCE_TYPE_WIDE"/>.</param>
    /// <param name="referenceHandle">the offset in the bytecode where the forward reference value must be
    /// stored.</param>
    private void AddForwardReference(int sourceInsnBytecodeOffset, int referenceType, int referenceHandle)
    {
        if (_forwardReferences == null)
        {
            _forwardReferences = new int[FORWARD_REFERENCES_CAPACITY_INCREMENT];
        }
        int[] forwardReferences = _forwardReferences;
        int lastElementIndex = forwardReferences[0];
        if (lastElementIndex + 2 >= forwardReferences.Length)
        {
            int[] newValues = new int[forwardReferences.Length + FORWARD_REFERENCES_CAPACITY_INCREMENT];
            Array.Copy(forwardReferences, 0, newValues, 0, forwardReferences.Length);
            _forwardReferences = newValues;
            forwardReferences = newValues;
        }
        forwardReferences[++lastElementIndex] = sourceInsnBytecodeOffset;
        forwardReferences[++lastElementIndex] = referenceType | referenceHandle;
        forwardReferences[0] = lastElementIndex;
    }

    /// <summary>
    /// Sets the bytecode offset of this label to the given value and resolves the forward references
    /// to this label, if any. This method must be called when this label is added to the bytecode of
    /// the method, i.e. when its bytecode offset becomes known. This method fills in the blanks that
    /// where left in the bytecode (and optionally in the stack map table) by each forward reference
    /// previously added to this label.
    /// </summary>
    /// <param name="code">the bytecode of the method.</param>
    /// <param name="stackMapTableEntries">the 'entries' array of the StackMapTable code attribute of the
    /// method. Maybe null.</param>
    /// <param name="bytecodeOffset">the bytecode offset of this label.</param>
    /// <returns>true if a blank that was left for this label was too small to store the
    /// offset. In such a case the corresponding jump instruction is replaced with an equivalent
    /// ASM specific instruction using an unsigned two bytes offset. These ASM specific
    /// instructions are later replaced with standard bytecode instructions with wider offsets (4
    /// bytes instead of 2), in ClassReader.</returns>
    internal bool Resolve(byte[] code, ByteVector? stackMapTableEntries, int bytecodeOffset)
    {
        this.Flags |= FLAG_RESOLVED;
        this.BytecodeOffset = bytecodeOffset;
        int[]? forwardReferences = _forwardReferences;
        if (forwardReferences == null)
        {
            return false;
        }
        bool hasAsmInstructions = false;
        for (int i = forwardReferences[0]; i > 0; i -= 2)
        {
            int sourceInsnBytecodeOffset = forwardReferences[i - 1];
            int reference = forwardReferences[i];
            int relativeOffset = bytecodeOffset - sourceInsnBytecodeOffset;
            int handle = reference & FORWARD_REFERENCE_HANDLE_MASK;
            if ((reference & FORWARD_REFERENCE_TYPE_MASK) == FORWARD_REFERENCE_TYPE_SHORT)
            {
                if (relativeOffset < short.MinValue || relativeOffset > short.MaxValue)
                {
                    // Change the opcode of the jump instruction, in order to be able to find it later in
                    // ClassReader. These ASM specific opcodes are similar to jump instruction opcodes, except
                    // that the 2 bytes offset is unsigned (and can therefore represent values from 0 to
                    // 65535, which is sufficient since the size of a method is limited to 65535 bytes).
                    int opcode = code[sourceInsnBytecodeOffset] & 0xFF;
                    if (opcode < Opcodes.IFNULL)
                    {
                        // Change IFEQ ... JSR to ASM_IFEQ ... ASM_JSR.
                        code[sourceInsnBytecodeOffset] = (byte)(opcode + ASM_OPCODE_DELTA);
                    }
                    else
                    {
                        // Change IFNULL and IFNONNULL to ASM_IFNULL and ASM_IFNONNULL.
                        code[sourceInsnBytecodeOffset] = (byte)(opcode + ASM_IFNULL_OPCODE_DELTA);
                    }
                    hasAsmInstructions = true;
                }
                code[handle++] = (byte)((int)((uint)relativeOffset >> 8));
                code[handle] = (byte)relativeOffset;
            }
            else if ((reference & FORWARD_REFERENCE_TYPE_MASK) == FORWARD_REFERENCE_TYPE_WIDE)
            {
                code[handle++] = (byte)((int)((uint)relativeOffset >> 24));
                code[handle++] = (byte)((int)((uint)relativeOffset >> 16));
                code[handle++] = (byte)((int)((uint)relativeOffset >> 8));
                code[handle] = (byte)relativeOffset;
            }
            else
            {
                stackMapTableEntries!.Data[handle++] = (byte)((int)((uint)bytecodeOffset >> 8));
                stackMapTableEntries.Data[handle] = (byte)bytecodeOffset;
            }
        }
        return hasAsmInstructions;
    }

    // -----------------------------------------------------------------------------------------------
    // Methods related to subroutines
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Finds the basic blocks that belong to the subroutine starting with the basic block
    /// corresponding to this label, and marks these blocks as belonging to this subroutine. This
    /// method follows the control flow graph to find all the blocks that are reachable from the
    /// current basic block WITHOUT following any jsr target.
    /// <para>
    /// Note: a precondition and postcondition of this method is that all labels must have a null
    /// <see cref="NextListElement"/>.
    /// </para>
    /// </summary>
    /// <param name="subroutineId">the id of the subroutine starting with the basic block corresponding to
    /// this label.</param>
    internal void MarkSubroutine(short subroutineId)
    {
        // Data flow algorithm: put this basic block in a list of blocks to process (which are blocks
        // belonging to subroutine subroutineId) and, while there are blocks to process, remove one from
        // the list, mark it as belonging to the subroutine, and add its successor basic blocks in the
        // control flow graph to the list of blocks to process (if not already done).
        Label listOfBlocksToProcess = this;
        listOfBlocksToProcess.NextListElement = EMPTY_LIST;
        while (listOfBlocksToProcess != EMPTY_LIST)
        {
            // Remove a basic block from the list of blocks to process.
            Label basicBlock = listOfBlocksToProcess;
            listOfBlocksToProcess = listOfBlocksToProcess.NextListElement!;
            basicBlock.NextListElement = null;

            // If it is not already marked as belonging to a subroutine, mark it as belonging to
            // subroutineId and add its successors to the list of blocks to process (unless already done).
            if (basicBlock.SubroutineId == 0)
            {
                basicBlock.SubroutineId = subroutineId;
                listOfBlocksToProcess = basicBlock.PushSuccessors(listOfBlocksToProcess);
            }
        }
    }

    /// <summary>
    /// Finds the basic blocks that end a subroutine starting with the basic block corresponding to
    /// this label and, for each one of them, adds an outgoing edge to the basic block following the
    /// given subroutine call. In other words, completes the control flow graph by adding the edges
    /// corresponding to the return from this subroutine, when called from the given caller basic
    /// block.
    /// <para>
    /// Note: a precondition and postcondition of this method is that all labels must have a null
    /// <see cref="NextListElement"/>.
    /// </para>
    /// </summary>
    /// <param name="subroutineCaller">a basic block that ends with a jsr to the basic block corresponding to
    /// this label. This label is supposed to correspond to the start of a subroutine.</param>
    internal void AddSubroutineRetSuccessors(Label subroutineCaller)
    {
        // Data flow algorithm: put this basic block in a list blocks to process (which are blocks
        // belonging to a subroutine starting with this label) and, while there are blocks to process,
        // remove one from the list, put it in a list of blocks that have been processed, add a return
        // edge to the successor of subroutineCaller if applicable, and add its successor basic blocks
        // in the control flow graph to the list of blocks to process (if not already done).
        Label listOfProcessedBlocks = EMPTY_LIST;
        Label listOfBlocksToProcess = this;
        listOfBlocksToProcess.NextListElement = EMPTY_LIST;
        while (listOfBlocksToProcess != EMPTY_LIST)
        {
            // Move a basic block from the list of blocks to process to the list of processed blocks.
            Label basicBlock = listOfBlocksToProcess;
            listOfBlocksToProcess = basicBlock.NextListElement!;
            basicBlock.NextListElement = listOfProcessedBlocks;
            listOfProcessedBlocks = basicBlock;

            // Add an edge from this block to the successor of the caller basic block, if this block is
            // the end of a subroutine and if this block and subroutineCaller do not belong to the same
            // subroutine.
            if ((basicBlock.Flags & FLAG_SUBROUTINE_END) != 0
                && basicBlock.SubroutineId != subroutineCaller.SubroutineId)
            {
                basicBlock.OutgoingEdges =
                    new Edge(
                        basicBlock.OutputStackSize,
                        // By construction, the first outgoing edge of a basic block that ends with a jsr
                        // instruction leads to the jsr continuation block, i.e. where execution continues
                        // when ret is called (see FLAG_SUBROUTINE_CALLER).
                        subroutineCaller.OutgoingEdges!.Successor,
                        basicBlock.OutgoingEdges);
            }
            // Add its successors to the list of blocks to process. Note that PushSuccessors does
            // not push basic blocks which are already in a list. Here this means either in the list of
            // blocks to process, or in the list of already processed blocks. This second list is
            // important to make sure we don't reprocess an already processed block.
            listOfBlocksToProcess = basicBlock.PushSuccessors(listOfBlocksToProcess);
        }
        // Reset the NextListElement of all the basic blocks that have been processed to null,
        // so that this method can be called again with a different subroutine or subroutine caller.
        while (listOfProcessedBlocks != EMPTY_LIST)
        {
            Label newListOfProcessedBlocks = listOfProcessedBlocks.NextListElement!;
            listOfProcessedBlocks.NextListElement = null;
            listOfProcessedBlocks = newListOfProcessedBlocks;
        }
    }

    /// <summary>
    /// Adds the successors of this label in the method's control flow graph (except those
    /// corresponding to a jsr target, and those already in a list of labels) to the given list of
    /// blocks to process, and returns the new list.
    /// </summary>
    /// <param name="listOfLabelsToProcess">a list of basic blocks to process, linked together with their
    /// <see cref="NextListElement"/> field.</param>
    /// <returns>the new list of blocks to process.</returns>
    private Label PushSuccessors(Label listOfLabelsToProcess)
    {
        Label newListOfLabelsToProcess = listOfLabelsToProcess;
        Edge? outgoingEdge = OutgoingEdges;
        while (outgoingEdge != null)
        {
            // By construction, the second outgoing edge of a basic block that ends with a jsr instruction
            // leads to the jsr target (see FLAG_SUBROUTINE_CALLER).
            bool isJsrTarget =
                (Flags & FLAG_SUBROUTINE_CALLER) != 0 && outgoingEdge == OutgoingEdges!.NextEdge;
            if (!isJsrTarget && outgoingEdge.Successor.NextListElement == null)
            {
                // Add this successor to the list of blocks to process, if it does not already belong to a
                // list of labels.
                outgoingEdge.Successor.NextListElement = newListOfLabelsToProcess;
                newListOfLabelsToProcess = outgoingEdge.Successor;
            }
            outgoingEdge = outgoingEdge.NextEdge;
        }
        return newListOfLabelsToProcess;
    }

    // -----------------------------------------------------------------------------------------------
    // Overridden Object methods
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns a string representation of this label.
    /// </summary>
    /// <returns>a string representation of this label.</returns>
    public override string ToString()
    {
        return "L" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
}
