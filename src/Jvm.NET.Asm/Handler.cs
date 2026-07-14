// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// Information about an exception handler. Corresponds to an element of the exception_table array of
/// a Code attribute, as defined in the Java Virtual Machine Specification (JVMS). Handler instances
/// can be chained together, with their <see cref="NextHandler"/> field, to describe a full JVMS
/// exception_table array.
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
/// <remarks>
/// See <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.3">JVMS 4.7.3</see>.
/// </remarks>
internal sealed class Handler
{
    /// <summary>
    /// The start_pc field of this JVMS exception_table entry. Corresponds to the beginning of the
    /// exception handler's scope (inclusive).
    /// </summary>
    internal readonly Label? StartPc;

    /// <summary>
    /// The end_pc field of this JVMS exception_table entry. Corresponds to the end of the exception
    /// handler's scope (exclusive).
    /// </summary>
    internal readonly Label? EndPc;

    /// <summary>
    /// The handler_pc field of this JVMS exception_table entry. Corresponding to the beginning of the
    /// exception handler's code.
    /// </summary>
    internal readonly Label? HandlerPc;

    /// <summary>
    /// The catch_type field of this JVMS exception_table entry. This is the constant pool index of the
    /// internal name of the type of exceptions handled by this handler, or 0 to catch any exceptions.
    /// </summary>
    internal readonly int CatchType;

    /// <summary>
    /// The internal name of the type of exceptions handled by this handler, or <c>null</c> to
    /// catch any exceptions.
    /// </summary>
    internal readonly string? CatchTypeDescriptor;

    /// <summary>The next exception handler.</summary>
    internal Handler? NextHandler;

    /// <summary>
    /// Constructs a new Handler.
    /// </summary>
    /// <param name="startPc">the start_pc field of this JVMS exception_table entry.</param>
    /// <param name="endPc">the end_pc field of this JVMS exception_table entry.</param>
    /// <param name="handlerPc">the handler_pc field of this JVMS exception_table entry.</param>
    /// <param name="catchType">The catch_type field of this JVMS exception_table entry.</param>
    /// <param name="catchTypeDescriptor">The internal name of the type of exceptions handled by this
    ///     handler, or <c>null</c> to catch any exceptions.</param>
    internal Handler(Label? startPc, Label? endPc, Label? handlerPc, int catchType, string? catchTypeDescriptor)
    {
        StartPc = startPc;
        EndPc = endPc;
        HandlerPc = handlerPc;
        CatchType = catchType;
        CatchTypeDescriptor = catchTypeDescriptor;
    }

    /// <summary>
    /// Constructs a new Handler from the given one, with a different scope.
    /// </summary>
    /// <param name="handler">an existing Handler.</param>
    /// <param name="startPc">the start_pc field of this JVMS exception_table entry.</param>
    /// <param name="endPc">the end_pc field of this JVMS exception_table entry.</param>
    internal Handler(Handler handler, Label? startPc, Label? endPc)
        : this(startPc, endPc, handler.HandlerPc, handler.CatchType, handler.CatchTypeDescriptor)
    {
        NextHandler = handler.NextHandler;
    }

    /// <summary>
    /// Removes the range between start and end from the Handler list that begins with the given
    /// element.
    /// </summary>
    /// <param name="firstHandler">the beginning of a Handler list. May be <c>null</c>.</param>
    /// <param name="start">the start of the range to be removed.</param>
    /// <param name="end">the end of the range to be removed. Maybe <c>null</c>.</param>
    /// <returns>the exception handler list with the start-end range removed.</returns>
    internal static Handler? RemoveRange(Handler? firstHandler, Label start, Label? end)
    {
        if (firstHandler == null)
        {
            return null;
        }
        else
        {
            firstHandler.NextHandler = RemoveRange(firstHandler.NextHandler, start, end);
        }
        int handlerStart = firstHandler.StartPc!.BytecodeOffset;
        int handlerEnd = firstHandler.EndPc!.BytecodeOffset;
        int rangeStart = start.BytecodeOffset;
        int rangeEnd = end == null ? int.MaxValue : end.BytecodeOffset;
        // Return early if [handlerStart,handlerEnd[ and [rangeStart,rangeEnd[ don't intersect.
        if (rangeStart >= handlerEnd || rangeEnd <= handlerStart)
        {
            return firstHandler;
        }
        if (rangeStart <= handlerStart)
        {
            if (rangeEnd >= handlerEnd)
            {
                // If [handlerStart,handlerEnd[ is included in [rangeStart,rangeEnd[, remove firstHandler.
                return firstHandler.NextHandler;
            }
            else
            {
                // [handlerStart,handlerEnd[ - [rangeStart,rangeEnd[ = [rangeEnd,handlerEnd[
                return new Handler(firstHandler, end, firstHandler.EndPc);
            }
        }
        else if (rangeEnd >= handlerEnd)
        {
            // [handlerStart,handlerEnd[ - [rangeStart,rangeEnd[ = [handlerStart,rangeStart[
            return new Handler(firstHandler, firstHandler.StartPc, start);
        }
        else
        {
            // [handlerStart,handlerEnd[ - [rangeStart,rangeEnd[ =
            //     [handlerStart,rangeStart[ + [rangeEnd,handerEnd[
            firstHandler.NextHandler = new Handler(firstHandler, end, firstHandler.EndPc);
            return new Handler(firstHandler, firstHandler.StartPc, start);
        }
    }

    /// <summary>
    /// Returns the number of elements of the Handler list that begins with the given element.
    /// </summary>
    /// <param name="firstHandler">the beginning of a Handler list. May be <c>null</c>.</param>
    /// <returns>the number of elements of the Handler list that begins with 'handler'.</returns>
    internal static int GetExceptionTableLength(Handler? firstHandler)
    {
        int length = 0;
        Handler? handler = firstHandler;
        while (handler != null)
        {
            length++;
            handler = handler.NextHandler;
        }
        return length;
    }

    /// <summary>
    /// Returns the size in bytes of the JVMS exception_table corresponding to the Handler list that
    /// begins with the given element. <i>This includes the exception_table_length field.</i>
    /// </summary>
    /// <param name="firstHandler">the beginning of a Handler list. May be <c>null</c>.</param>
    /// <returns>the size in bytes of the exception_table_length and exception_table structures.</returns>
    internal static int GetExceptionTableSize(Handler? firstHandler)
    {
        return 2 + 8 * GetExceptionTableLength(firstHandler);
    }

    /// <summary>
    /// Puts the JVMS exception_table corresponding to the Handler list that begins with the given
    /// element. <i>This includes the exception_table_length field.</i>
    /// </summary>
    /// <param name="firstHandler">the beginning of a Handler list. May be <c>null</c>.</param>
    /// <param name="output">where the exception_table_length and exception_table structures must be put.</param>
    internal static void PutExceptionTable(Handler? firstHandler, ByteVector output)
    {
        output.PutShort(GetExceptionTableLength(firstHandler));
        Handler? handler = firstHandler;
        while (handler != null)
        {
            output
                .PutShort(handler.StartPc!.BytecodeOffset)
                .PutShort(handler.EndPc!.BytecodeOffset)
                .PutShort(handler.HandlerPc!.BytecodeOffset)
                .PutShort(handler.CatchType);
            handler = handler.NextHandler;
        }
    }
}
