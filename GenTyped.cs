// GenTyped.cs - C/C++/C#/Java code generator
//
// Copyright (C) 2011-2022  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public abstract class GenTyped : GenBase
{
	protected abstract void Write(TypeCode typeCode);

	protected abstract void Write(CiType type, bool promote);

	protected override void WriteTypeAndName(CiNamedValue value)
	{
		Write(value.Type, true);
		Write(' ');
		WriteName(value);
	}

	public override CiExpr Visit(CiAggregateInitializer expr, CiPriority parent)
	{
		CiType type = ((CiArrayStorageType) expr.Type).ElementType;
		Write("{ ");
		WriteCoercedLiterals(type, expr.Items);
		Write(" }");
		return expr;
	}

	protected override void WriteNewArray(CiType elementType, CiExpr lengthExpr, CiPriority parent)
	{
		Write("new ");
		Write(elementType.BaseType, false);
		Write('[');
		lengthExpr.Accept(this, CiPriority.Argument);
		Write(']');
		while (elementType is CiArrayType array) {
			Write('[');
			if (array is CiArrayStorageType arrayStorage)
				arrayStorage.LengthExpr.Accept(this, CiPriority.Argument);
			Write(']');
			elementType = array.ElementType;
		}
	}

	static bool IsAscii(long c)
	{
		if (c >= ' ' && c <= 0x7e)
			return true;
		switch (c) {
		case '\a':
		case '\b':
		case '\f':
		case '\n':
		case '\r':
		case '\t':
		case '\v':
			return true;
		default:
			return false;
		}
	}

	protected bool IsOneAsciiString(CiExpr expr, out char c)
	{
		if (expr is CiLiteralString literal && literal.Value.Length == 1 && IsAscii(literal.Value[0])) {
			c = literal.Value[0];
			return true;
		}
		c = '\0';
		return false;
	}

	protected void WriteCharLiteral(char c)
	{
		Write('\'');
		if (c == '\'')
			Write('\\');
		WriteEscapedChar(c);
		Write('\'');
	}

	protected override void WriteComparison(CiBinaryExpr expr, CiPriority parent, CiPriority child, string op)
	{
		if (expr.Left.IsIndexing && expr.Left is CiBinaryExpr indexing && indexing.Left.Type is CiStringType
		 && expr.Right is CiLiteralLong literal && IsAscii(literal.Value)) {
			if (parent > child)
				Write('(');
			expr.Left.Accept(this, child);
			Write(op);
			WriteCharLiteral((char) literal.Value);
			if (parent > child)
				Write(')');
		}
		else
			base.WriteComparison(expr, parent, child, op);
	}

	protected static bool IsNarrower(TypeCode left, TypeCode right)
	{
		switch (left) {
		case TypeCode.SByte:
			switch (right) {
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Byte:
			switch (right) {
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int16:
			switch (right) {
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.UInt16:
			switch (right) {
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int32:
			return right == TypeCode.Int64;
		default:
			return false;
		}
	}

	protected CiExpr GetStaticCastInner(CiType type, CiExpr expr)
	{
		if (expr is CiBinaryExpr binary && binary.Op == CiToken.And && binary.Right is CiLiteralLong rightMask
		 && type is CiIntegerType integer) {
			long mask;
			switch (GetIntegerTypeCode(integer, false)) {
			case TypeCode.Byte:
			case TypeCode.SByte:
				mask = 0xff;
				break;
			case TypeCode.Int16:
			case TypeCode.UInt16:
				mask = 0xffff;
				break;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				mask = 0xffffffff;
				break;
			default:
				return expr;
			}
			if ((rightMask.Value & mask) == mask)
				return binary.Left;
		}
		return expr;
	}

	protected virtual void WriteStaticCast(CiType type, CiExpr expr)
	{
		Write('(');
		Write(type, false);
		Write(") ");
		GetStaticCastInner(type, expr).Accept(this, CiPriority.Primary);
	}

	protected override void WriteNotPromoted(CiType type, CiExpr expr)
	{
		if (type is CiIntegerType elementType
		 && IsNarrower(GetIntegerTypeCode(elementType, false), GetIntegerTypeCode((CiIntegerType) expr.Type, true)))
			WriteStaticCast(elementType, expr);
		else
			expr.Accept(this, CiPriority.Argument);
	}

	protected virtual bool IsNotPromotedIndexing(CiBinaryExpr expr) => expr.Op == CiToken.LeftBracket;

	protected override void WriteAssignRight(CiBinaryExpr expr)
	{
		if (expr.Left.IsIndexing) {
			TypeCode leftTypeCode = GetTypeCode(expr.Left.Type, false);
			bool promote;
			switch (expr.Right) {
			case CiLiteral _:
			case CiBinaryExpr rightBinary when IsNotPromotedIndexing(rightBinary) || rightBinary.IsAssign:
				promote = false;
				break;
			default:
				promote = true;
				break;
			}
			TypeCode rightTypeCode = GetTypeCode(expr.Right.Type, promote);
			if (leftTypeCode == TypeCode.SByte && rightTypeCode == TypeCode.SByte) {
				expr.Right.Accept(this, CiPriority.Assign); // omit Java "& 0xff"
				return;
			}
			if (IsNarrower(leftTypeCode, rightTypeCode)) {
				WriteStaticCast(expr.Left.Type, expr.Right);
				return;
			}
		}
		WriteCoerced(expr.Left.Type, expr.Right, CiPriority.Argument);
	}

	protected override void WriteCoercedInternal(CiType type, CiExpr expr, CiPriority parent)
	{
		if (type is CiIntegerType && type != CiSystem.LongType && expr.Type == CiSystem.LongType)
			WriteStaticCast(type, expr);
		else if (type == CiSystem.FloatType && expr.Type == CiSystem.DoubleType) {
			if (expr is CiLiteral) {
				expr.Accept(this, CiPriority.Argument);
				Write('f');
			}
			else
				WriteStaticCast(type, expr);
		}
		else if (type is CiIntegerType && expr.Type == CiSystem.FloatIntType) {
			if (expr is CiCallExpr call && call.Method.IsReferenceTo(CiSystem.MathTruncate))
				WriteStaticCast(type, call.Arguments[0]);
			else
				WriteStaticCast(type, expr);
		}
		else
			base.WriteCoercedInternal(type, expr, parent);
	}

	protected override void WriteCharAt(CiBinaryExpr expr)
	{
		WriteIndexing(expr, CiPriority.Argument);
	}

	protected abstract bool HasInitCode(CiNamedValue def);

	protected virtual bool NeedsConstructor(CiClass klass)
	{
		return klass.Constructor != null
			|| klass.Fields.Any(field => HasInitCode(field));
	}

}

}
