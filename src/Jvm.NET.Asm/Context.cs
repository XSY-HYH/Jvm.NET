// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// Information about a class being parsed in a <see cref="ClassReader"/>.
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
internal sealed class Context
{
    /// <summary>The Prototypes of the attributes that must be parsed in this class.</summary>
    internal Attribute[]? AttributePrototypes;

    /// <summary>
    /// The options used to parse this class. One or more of <see cref="ClassReader.SKIP_CODE"/>,
    /// <see cref="ClassReader.SKIP_DEBUG"/>, <see cref="ClassReader.SKIP_FRAMES"/>,
    /// <see cref="ClassReader.EXPAND_FRAMES"/> or <see cref="ClassReader.EXPAND_ASM_INSNS"/>.
    /// </summary>
    internal int ParsingOptions;

    /// <summary>The buffer used to read strings in the constant pool.</summary>
    internal char[]? CharBuffer;

    // Information about the current method, i.e. the one read in the current (or latest) call
    // to ClassReader.readMethod().

    /// <summary>The access flags of the current method.</summary>
    internal int CurrentMethodAccessFlags;

    /// <summary>The name of the current method.</summary>
    internal string? CurrentMethodName;

    /// <summary>The descriptor of the current method.</summary>
    internal string? CurrentMethodDescriptor;

    /// <summary>
    /// The labels of the current method, indexed by bytecode offset (only bytecode offsets for which a
    /// label is needed have a non null associated Label).
    /// </summary>
    internal Label[]? CurrentMethodLabels;

    // Information about the current type annotation target, i.e. the one read in the current
    // (or latest) call to ClassReader.readAnnotationTarget().

    /// <summary>
    /// The target_type and target_info of the current type annotation target, encoded as described in
    /// <see cref="TypeReference"/>.
    /// </summary>
    internal int CurrentTypeAnnotationTarget;

    /// <summary>The target_path of the current type annotation target.</summary>
    internal TypePath? CurrentTypeAnnotationTargetPath;

    /// <summary>The start of each local variable range in the current local variable annotation.</summary>
    internal Label[]? CurrentLocalVariableAnnotationRangeStarts;

    /// <summary>The end of each local variable range in the current local variable annotation.</summary>
    internal Label[]? CurrentLocalVariableAnnotationRangeEnds;

    /// <summary>
    /// The local variable index of each local variable range in the current local variable annotation.
    /// </summary>
    internal int[]? CurrentLocalVariableAnnotationRangeIndices;

    // Information about the current stack map frame, i.e. the one read in the current (or latest)
    // call to ClassReader.readFrame().

    /// <summary>The bytecode offset of the current stack map frame.</summary>
    internal int CurrentFrameOffset;

    /// <summary>
    /// The type of the current stack map frame. One of <see cref="Opcodes.F_FULL"/>,
    /// <see cref="Opcodes.F_APPEND"/>, <see cref="Opcodes.F_CHOP"/>, <see cref="Opcodes.F_SAME"/>
    /// or <see cref="Opcodes.F_SAME1"/>.
    /// </summary>
    internal int CurrentFrameType;

    /// <summary>
    /// The number of local variable types in the current stack map frame. Each type is represented
    /// with a single array element (even long and double).
    /// </summary>
    internal int CurrentFrameLocalCount;

    /// <summary>
    /// The delta number of local variable types in the current stack map frame (each type is
    /// represented with a single array element - even long and double). This is the number of local
    /// variable types in this frame, minus the number of local variable types in the previous frame.
    /// </summary>
    internal int CurrentFrameLocalCountDelta;

    /// <summary>
    /// The types of the local variables in the current stack map frame. Each type is represented with
    /// a single array element (even long and double), using the format described in
    /// MethodVisitor.visitFrame. Depending on <see cref="CurrentFrameType"/>, this contains the types of
    /// all the local variables, or only those of the additional ones (compared to the previous frame).
    /// </summary>
    internal object[]? CurrentFrameLocalTypes;

    /// <summary>
    /// The number stack element types in the current stack map frame. Each type is represented with a
    /// single array element (even long and double).
    /// </summary>
    internal int CurrentFrameStackCount;

    /// <summary>
    /// The types of the stack elements in the current stack map frame. Each type is represented with a
    /// single array element (even long and double), using the format described in
    /// MethodVisitor.visitFrame.
    /// </summary>
    internal object[]? CurrentFrameStackTypes;
}
