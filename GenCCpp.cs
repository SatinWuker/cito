// GenCCpp.cs - C/C++ code generator
//
// Copyright (C) 2011-2019  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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

namespace Foxoft.Ci
{

public abstract class GenCCpp : GenTyped
{
	protected readonly Dictionary<CiClass, bool> WrittenClasses = new Dictionary<CiClass, bool>();
	protected CiMethod CurrentMethod;

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte: Write("int8_t"); break;
		case TypeCode.Byte: Write("uint8_t"); break;
		case TypeCode.Int16: Write("int16_t"); break;
		case TypeCode.UInt16: Write("uint16_t"); break;
		case TypeCode.Int32: Write("int"); break;
		case TypeCode.Int64: Write("int64_t"); break;
		default: throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void WriteClassStorageInit(CiClass klass)
	{
	}

	protected override void WriteReturnValue(CiExpr expr)
	{
		WriteCoerced(this.CurrentMethod.Type, expr, CiPriority.Statement);
	}
}

}
