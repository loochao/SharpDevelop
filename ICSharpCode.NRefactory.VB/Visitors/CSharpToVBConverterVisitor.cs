﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.VB.Ast;

namespace ICSharpCode.NRefactory.VB.Visitors
{
	public interface IEnvironmentProvider
	{
		string RootNamespace { get; }
		string GetTypeNameForAttribute(CSharp.Attribute attribute);
		ClassType GetClassTypeForAstType(CSharp.AstType type);
		IType ResolveExpression(CSharp.Expression expression);
	}
	
	/// <summary>
	/// Description of CSharpToVBConverterVisitor.
	/// </summary>
	public class CSharpToVBConverterVisitor : CSharp.IAstVisitor<object, VB.AstNode>
	{
		IEnvironmentProvider provider;
		Stack<BlockStatement> blocks;
		// TODO this should belong to the current type member or lambda
		bool inIterator;
		
		public CSharpToVBConverterVisitor(IEnvironmentProvider provider)
		{
			this.provider = provider;
			this.blocks = new Stack<BlockStatement>();
		}
		
		public AstNode VisitAnonymousMethodExpression(CSharp.AnonymousMethodExpression anonymousMethodExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitUndocumentedExpression(CSharp.UndocumentedExpression undocumentedExpression, object data)
		{
			// 29.06.2011 20:28:36 Siegfried Pammer: wie soll ich die behandeln?
			// 29.06.2011 20:28:58 Siegfried Pammer: throw new NotSupportedException(); ?
			// 29.06.2011 20:35:50 Daniel Grunwald: da würde ich wieder Pseudo-Funktionen einführen
			throw new NotImplementedException();
		}
		
		public AstNode VisitArrayCreateExpression(CSharp.ArrayCreateExpression arrayCreateExpression, object data)
		{
			var expr = new ArrayCreateExpression() {
				Type = (AstType)arrayCreateExpression.Type.AcceptVisitor(this, data),
				Initializer = (ArrayInitializerExpression)arrayCreateExpression.Initializer.AcceptVisitor(this, data)
			};
			ConvertNodes(arrayCreateExpression.Arguments, expr.Arguments);
			ConvertNodes(arrayCreateExpression.AdditionalArraySpecifiers, expr.AdditionalArraySpecifiers);
			
			return EndNode(arrayCreateExpression, expr);
		}
		
		public AstNode VisitArrayInitializerExpression(CSharp.ArrayInitializerExpression arrayInitializerExpression, object data)
		{
			var expr = new ArrayInitializerExpression();
			ConvertNodes(arrayInitializerExpression.Elements, expr.Elements);
			
			return EndNode(arrayInitializerExpression, expr);
		}
		
		public AstNode VisitAsExpression(CSharp.AsExpression asExpression, object data)
		{
			return EndNode(asExpression, new CastExpression(CastType.TryCast, (AstType)asExpression.Type.AcceptVisitor(this, data), (Expression)asExpression.Expression.AcceptVisitor(this, data)));
		}
		
		public AstNode VisitAssignmentExpression(CSharp.AssignmentExpression assignmentExpression, object data)
		{
			var left = (Expression)assignmentExpression.Left.AcceptVisitor(this, data);
			var op = AssignmentOperatorType.None;
			
			switch (assignmentExpression.Operator) {
				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Assign:
					op = AssignmentOperatorType.Assign;
					break;
				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Add:
					op = AssignmentOperatorType.Add;
					break;
				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Subtract:
					op = AssignmentOperatorType.Subtract;
					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Multiply:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Divide:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.Modulus:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.ShiftLeft:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.ShiftRight:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.BitwiseAnd:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.BitwiseOr:
//
//					break;
//				case ICSharpCode.NRefactory.CSharp.AssignmentOperatorType.ExclusiveOr:
//
//					break;
				default:
					throw new Exception("Invalid value for AssignmentOperatorType: " + assignmentExpression.Operator);
			}
			
			var right = (Expression)assignmentExpression.Right.AcceptVisitor(this, data);
			
			var expr = new AssignmentExpression(left, op, right);
			return EndNode(assignmentExpression, expr);
		}
		
		public AstNode VisitBaseReferenceExpression(CSharp.BaseReferenceExpression baseReferenceExpression, object data)
		{
			InstanceExpression result = new InstanceExpression(InstanceExpressionType.MyBase, ConvertLocation(baseReferenceExpression.StartLocation));
			
			return EndNode(baseReferenceExpression, result);
		}
		
		public AstNode VisitBinaryOperatorExpression(CSharp.BinaryOperatorExpression binaryOperatorExpression, object data)
		{
			var left = (Expression)binaryOperatorExpression.Left.AcceptVisitor(this, data);
			var op = BinaryOperatorType.None;
			var right = (Expression)binaryOperatorExpression.Right.AcceptVisitor(this, data);
			
			// TODO obj <> Nothing is wrong; correct would be obj IsNot Nothing
			
			switch (binaryOperatorExpression.Operator) {
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.BitwiseAnd:
					op = BinaryOperatorType.BitwiseAnd;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.BitwiseOr:
					op = BinaryOperatorType.BitwiseOr;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.ConditionalAnd:
					op = BinaryOperatorType.LogicalAnd;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.ConditionalOr:
					op = BinaryOperatorType.LogicalOr;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.ExclusiveOr:
					op = BinaryOperatorType.ExclusiveOr;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.GreaterThan:
					op = BinaryOperatorType.GreaterThan;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.GreaterThanOrEqual:
					op = BinaryOperatorType.GreaterThanOrEqual;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Equality:
					op = BinaryOperatorType.Equality;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.InEquality:
					op = BinaryOperatorType.InEquality;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.LessThan:
					op = BinaryOperatorType.LessThan;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.LessThanOrEqual:
					op = BinaryOperatorType.LessThanOrEqual;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Add:
					op = BinaryOperatorType.Add;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Subtract:
					op = BinaryOperatorType.Subtract;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Multiply:
					op = BinaryOperatorType.Multiply;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Divide:
					op = BinaryOperatorType.Divide;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.Modulus:
					op = BinaryOperatorType.Modulus;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.ShiftLeft:
					op = BinaryOperatorType.ShiftLeft;
					break;
				case ICSharpCode.NRefactory.CSharp.BinaryOperatorType.ShiftRight:
					op = BinaryOperatorType.ShiftRight;
					break;
				default:
					throw new Exception("Invalid value for BinaryOperatorType: " + binaryOperatorExpression.Operator);
			}
			
			return EndNode(binaryOperatorExpression, new BinaryOperatorExpression(left, op, right));
		}
		
		public AstNode VisitCastExpression(CSharp.CastExpression castExpression, object data)
		{
			var expr = new CastExpression();
			
			expr.Type = (AstType)castExpression.Type.AcceptVisitor(this, data);
			// TODO read additional type information from annotation
			// (int)x is equivalent to CInt(Math.Truncate(x))
			expr.CastType = GetCastType(expr.Type, null);
			expr.Expression = (Expression)castExpression.Expression.AcceptVisitor(this, data);
			
			if (expr.CastType != CastType.CType)
				expr.Type = null;
			
			return EndNode(castExpression, expr);
		}
		
		CastType GetCastType(AstType type, object typeInformation)
		{
			var primType = type as PrimitiveType;
			if (primType == null)
				return CastType.CType;
			
			switch (primType.Keyword) {
				case "Integer":
					return CastType.CInt;
				case "String":
					return CastType.CStr;
			}
			
			return CastType.CType;
		}
		
		public AstNode VisitCheckedExpression(CSharp.CheckedExpression checkedExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitConditionalExpression(CSharp.ConditionalExpression conditionalExpression, object data)
		{
			var cond = new ConditionalExpression() {
				ConditionExpression = (Expression)conditionalExpression.Condition.AcceptVisitor(this, data),
				TrueExpression = (Expression)conditionalExpression.TrueExpression.AcceptVisitor(this, data),
				FalseExpression = (Expression)conditionalExpression.FalseExpression.AcceptVisitor(this, data)
			};
			
			return EndNode(conditionalExpression, cond);
		}
		
		public AstNode VisitDefaultValueExpression(CSharp.DefaultValueExpression defaultValueExpression, object data)
		{
			// Nothing is equivalent to default(T) for reference and value types.
			return EndNode(defaultValueExpression, new PrimitiveExpression(null));
		}
		
		public AstNode VisitDirectionExpression(CSharp.DirectionExpression directionExpression, object data)
		{
			return EndNode(directionExpression, (Expression)directionExpression.Expression.AcceptVisitor(this, data));
		}
		
		public AstNode VisitIdentifierExpression(CSharp.IdentifierExpression identifierExpression, object data)
		{
			var expr = new IdentifierExpression();
			expr.Identifier = new Identifier(identifierExpression.Identifier, AstLocation.Empty);
			ConvertNodes(identifierExpression.TypeArguments, expr.TypeArguments);
			
			return EndNode(identifierExpression, expr);
		}
		
		public AstNode VisitIndexerExpression(CSharp.IndexerExpression indexerExpression, object data)
		{
			var expr = new InvocationExpression((Expression)indexerExpression.Target.AcceptVisitor(this, data));
			ConvertNodes(indexerExpression.Arguments, expr.Arguments);
			return EndNode(indexerExpression, expr);
		}
		
		public AstNode VisitInvocationExpression(CSharp.InvocationExpression invocationExpression, object data)
		{
			var expr = new InvocationExpression(
				(Expression)invocationExpression.Target.AcceptVisitor(this, data));
			ConvertNodes(invocationExpression.Arguments, expr.Arguments);
			
			return EndNode(invocationExpression, expr);
		}
		
		public AstNode VisitIsExpression(CSharp.IsExpression isExpression, object data)
		{
			var expr = new TypeOfIsExpression() {
				Type = (AstType)isExpression.Type.AcceptVisitor(this, data),
				TypeOfExpression = (Expression)isExpression.Expression.AcceptVisitor(this, data)
			};
			
			return EndNode(isExpression, expr);
		}
		
		public AstNode VisitLambdaExpression(CSharp.LambdaExpression lambdaExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitMemberReferenceExpression(CSharp.MemberReferenceExpression memberReferenceExpression, object data)
		{
			var memberAccessExpression = new MemberAccessExpression();
			
			memberAccessExpression.Target = (Expression)memberReferenceExpression.Target.AcceptVisitor(this, data);
			memberAccessExpression.Member = new Identifier(memberReferenceExpression.MemberName, AstLocation.Empty);
			ConvertNodes(memberReferenceExpression.TypeArguments, memberAccessExpression.TypeArguments);
			
			return EndNode(memberReferenceExpression, memberAccessExpression);
		}
		
		public AstNode VisitNamedArgumentExpression(CSharp.NamedArgumentExpression namedArgumentExpression, object data)
		{
			Expression expr;
			
			if (namedArgumentExpression.Parent is CSharp.ArrayInitializerExpression) {
				expr = new FieldInitializerExpression {
					IsKey = true,
					Identifier = namedArgumentExpression.Identifier,
					Expression = (Expression)namedArgumentExpression.Expression.AcceptVisitor(this, data)
				};
			} else {
				expr = new NamedArgumentExpression {
					Identifier = namedArgumentExpression.Identifier,
					Expression = (Expression)namedArgumentExpression.Expression.AcceptVisitor(this, data)
				};
			}
			
			return EndNode(namedArgumentExpression, expr);
		}
		
		public AstNode VisitNullReferenceExpression(CSharp.NullReferenceExpression nullReferenceExpression, object data)
		{
			return EndNode(nullReferenceExpression, new PrimitiveExpression(null));
		}
		
		public AstNode VisitObjectCreateExpression(CSharp.ObjectCreateExpression objectCreateExpression, object data)
		{
			var expr = new ObjectCreationExpression((AstType)objectCreateExpression.Type.AcceptVisitor(this, data));
			ConvertNodes(objectCreateExpression.Arguments, expr.Arguments);
			if (!objectCreateExpression.Initializer.IsNull)
				expr.Initializer = (ArrayInitializerExpression)objectCreateExpression.Initializer.AcceptVisitor(this, data);
			
			return EndNode(objectCreateExpression, expr);
		}
		
		public AstNode VisitAnonymousTypeCreateExpression(CSharp.AnonymousTypeCreateExpression anonymousTypeCreateExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitParenthesizedExpression(CSharp.ParenthesizedExpression parenthesizedExpression, object data)
		{
			var result = new ParenthesizedExpression();
			
			result.Expression = (Expression)parenthesizedExpression.Expression.AcceptVisitor(this, data);
			
			return EndNode(parenthesizedExpression, result);
		}
		
		public AstNode VisitPointerReferenceExpression(CSharp.PointerReferenceExpression pointerReferenceExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitPrimitiveExpression(CSharp.PrimitiveExpression primitiveExpression, object data)
		{
			Expression expr;
			
			if (primitiveExpression.Value is string && ((string)primitiveExpression.Value).IndexOfAny(new[] {'\r', '\n'}) > -1)
				expr = ConvertToConcat((string)primitiveExpression.Value);
			else
				expr = new PrimitiveExpression(primitiveExpression.Value);
			
			return EndNode(primitiveExpression, expr);
		}
		
		Expression ConvertToConcat(string literal)
		{
			Stack<Expression> parts = new Stack<Expression>();
			int start = 0;
			
			for (int i = 0; i < literal.Length; i++) {
				if (literal[i] == '\r') {
					string part = literal.Substring(start, i - start);
					parts.Push(new PrimitiveExpression(part));
					if (i + 1 < literal.Length && literal[i + 1] == '\n') {
						i++;
						parts.Push(new IdentifierExpression() { Identifier = "vbCrLf" });
					} else
						parts.Push(new IdentifierExpression() { Identifier = "vbCr" });
					start = i + 1;
				} else if (literal[i] == '\n') {
					string part = literal.Substring(start, i - start);
					parts.Push(new PrimitiveExpression(part));
					parts.Push(new IdentifierExpression() { Identifier = "vbLf" });
					start = i + 1;
				}
			}
			
			if (start < literal.Length) {
				string part = literal.Substring(start);
				parts.Push(new PrimitiveExpression(part));
			}
			
			Expression current = parts.Pop();
			
			while (parts.Any())
				current = new BinaryOperatorExpression(parts.Pop(), BinaryOperatorType.Concat, current);
			
			return current;
		}
		
		public AstNode VisitSizeOfExpression(CSharp.SizeOfExpression sizeOfExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitStackAllocExpression(CSharp.StackAllocExpression stackAllocExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitThisReferenceExpression(CSharp.ThisReferenceExpression thisReferenceExpression, object data)
		{
			InstanceExpression result = new InstanceExpression(InstanceExpressionType.Me, ConvertLocation(thisReferenceExpression.StartLocation));
			
			return EndNode(thisReferenceExpression, result);
		}
		
		public AstNode VisitTypeOfExpression(CSharp.TypeOfExpression typeOfExpression, object data)
		{
			var expr = new GetTypeExpression();
			expr.Type = (AstType)typeOfExpression.Type.AcceptVisitor(this, data);
			return EndNode(typeOfExpression, expr);
		}
		
		public AstNode VisitTypeReferenceExpression(CSharp.TypeReferenceExpression typeReferenceExpression, object data)
		{
			var expr = new TypeReferenceExpression((AstType)typeReferenceExpression.Type.AcceptVisitor(this, data));
			return EndNode(typeReferenceExpression, expr);
		}
		
		public AstNode VisitUnaryOperatorExpression(CSharp.UnaryOperatorExpression unaryOperatorExpression, object data)
		{
			Expression expr;

			switch (unaryOperatorExpression.Operator) {
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Not:
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.BitNot:
					expr = new UnaryOperatorExpression() {
						Expression = (Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data),
						Operator = UnaryOperatorType.Not
					};
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Minus:
					expr = new UnaryOperatorExpression() {
						Expression = (Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data),
						Operator = UnaryOperatorType.Minus
					};
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Plus:
					expr = new UnaryOperatorExpression() {
						Expression = (Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data),
						Operator = UnaryOperatorType.Plus
					};
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Increment:
					expr = new InvocationExpression();
					((InvocationExpression)expr).Target = new IdentifierExpression() { Identifier = "Increment" };
					((InvocationExpression)expr).Arguments.Add((Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data));
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.PostIncrement:
					expr = new InvocationExpression();
					((InvocationExpression)expr).Target = new IdentifierExpression() { Identifier = "PostIncrement" };
					((InvocationExpression)expr).Arguments.Add((Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data));
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Decrement:
					expr = new InvocationExpression();
					((InvocationExpression)expr).Target = new IdentifierExpression() { Identifier = "Decrement" };
					((InvocationExpression)expr).Arguments.Add((Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data));
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.PostDecrement:
					expr = new InvocationExpression();
					((InvocationExpression)expr).Target = new IdentifierExpression() { Identifier = "PostDecrement" };
					((InvocationExpression)expr).Arguments.Add((Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data));
					break;
				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.AddressOf:
					expr = new AddressOfExpression() {
						Expression = (Expression)unaryOperatorExpression.Expression.AcceptVisitor(this, data)
					};
					break;
//				case ICSharpCode.NRefactory.CSharp.UnaryOperatorType.Dereference:
				default:
					throw new Exception("Invalid value for UnaryOperatorType");
			}
			
			return EndNode(unaryOperatorExpression, expr);
		}
		
		public AstNode VisitUncheckedExpression(CSharp.UncheckedExpression uncheckedExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitEmptyExpression(CSharp.EmptyExpression emptyExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryExpression(CSharp.QueryExpression queryExpression, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryContinuationClause(CSharp.QueryContinuationClause queryContinuationClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryFromClause(CSharp.QueryFromClause queryFromClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryLetClause(CSharp.QueryLetClause queryLetClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryWhereClause(CSharp.QueryWhereClause queryWhereClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryJoinClause(CSharp.QueryJoinClause queryJoinClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryOrderClause(CSharp.QueryOrderClause queryOrderClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryOrdering(CSharp.QueryOrdering queryOrdering, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQuerySelectClause(CSharp.QuerySelectClause querySelectClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitQueryGroupClause(CSharp.QueryGroupClause queryGroupClause, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitAttribute(CSharp.Attribute attribute, object data)
		{
			var attr = new VB.Ast.Attribute();
			
			// TODO : attribute targets
			
			attr.Type = (AstType)attribute.Type.AcceptVisitor(this, data);
			ConvertNodes(attribute.Arguments, attr.Arguments);
			
			return EndNode(attribute, attr);
		}
		
		public AstNode VisitAttributeSection(CSharp.AttributeSection attributeSection, object data)
		{
			AttributeBlock block = new AttributeBlock();
			ConvertNodes(attributeSection.Attributes, block.Attributes);
			return EndNode(attributeSection, block);
		}
		
		public AstNode VisitDelegateDeclaration(CSharp.DelegateDeclaration delegateDeclaration, object data)
		{
			var result = new DelegateDeclaration();
			
			ConvertNodes(delegateDeclaration.Attributes.Where(section => section.AttributeTarget != "return"), result.Attributes);
			ConvertNodes(delegateDeclaration.ModifierTokens, result.ModifierTokens);
			result.Name = new Identifier(delegateDeclaration.Name, AstLocation.Empty);
			result.IsSub = IsSub(delegateDeclaration.ReturnType);
			ConvertNodes(delegateDeclaration.Parameters, result.Parameters);
			ConvertNodes(delegateDeclaration.TypeParameters, result.TypeParameters);
			ConvertNodes(delegateDeclaration.Attributes.Where(section => section.AttributeTarget == "return"), result.ReturnTypeAttributes);
			if (!result.IsSub)
				result.ReturnType = (AstType)delegateDeclaration.ReturnType.AcceptVisitor(this, data);
			return EndNode(delegateDeclaration, result);
		}
		
		public AstNode VisitNamespaceDeclaration(CSharp.NamespaceDeclaration namespaceDeclaration, object data)
		{
			var newNamespace = new NamespaceDeclaration();
			
			ConvertNodes(namespaceDeclaration.Identifiers, newNamespace.Identifiers);
			ConvertNodes(namespaceDeclaration.Members, newNamespace.Members);
			
			return EndNode(namespaceDeclaration, newNamespace);
		}
		
		public AstNode VisitTypeDeclaration(CSharp.TypeDeclaration typeDeclaration, object data)
		{
			// TODO add missing features!
			
			if (typeDeclaration.ClassType == ClassType.Enum) {
				var type = new EnumDeclaration();
				
				ConvertNodes(typeDeclaration.Attributes, type.Attributes);
				ConvertNodes(typeDeclaration.ModifierTokens, type.ModifierTokens);
				
				if (typeDeclaration.BaseTypes.Any()) {
					var first = typeDeclaration.BaseTypes.First();
					
					type.UnderlyingType = (AstType)first.AcceptVisitor(this, data);
				}
				
				type.Name = new Identifier(typeDeclaration.Name, AstLocation.Empty);
				
				ConvertNodes(typeDeclaration.Members, type.Members);
				
				return EndNode(typeDeclaration, type);
			} else {
				var type = new TypeDeclaration();
				
				CSharp.Attribute stdModAttr;
				
				if (typeDeclaration.ClassType == ClassType.Class && HasAttribute(typeDeclaration.Attributes, "Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute", out stdModAttr)) {
					type.ClassType = ClassType.Module;
					// remove AttributeSection if only one attribute is present
					var attrSec = (CSharp.AttributeSection)stdModAttr.Parent;
					if (attrSec.Attributes.Count == 1)
						attrSec.Remove();
					else
						stdModAttr.Remove();
				} else
					type.ClassType = typeDeclaration.ClassType;
				
				ConvertNodes(typeDeclaration.Attributes, type.Attributes);
				ConvertNodes(typeDeclaration.ModifierTokens, type.ModifierTokens);
				
				if (typeDeclaration.BaseTypes.Any()) {
					var first = typeDeclaration.BaseTypes.First();
					
					if (provider.GetClassTypeForAstType(first) != ClassType.Interface) {
						ConvertNodes(typeDeclaration.BaseTypes.Skip(1), type.ImplementsTypes);
						type.InheritsType = (AstType)first.AcceptVisitor(this, data);
					} else
						ConvertNodes(typeDeclaration.BaseTypes, type.ImplementsTypes);
				}
				
				type.Name = new Identifier(typeDeclaration.Name, AstLocation.Empty);
				
				ConvertNodes(typeDeclaration.Members, type.Members);
				
				return EndNode(typeDeclaration, type);
			}
		}
		
		public AstNode VisitUsingAliasDeclaration(CSharp.UsingAliasDeclaration usingAliasDeclaration, object data)
		{
			var imports = new ImportsStatement();
			
			var clause = new AliasImportsClause() {
				Name = new Identifier(usingAliasDeclaration.Alias, AstLocation.Empty),
				Alias = (AstType)usingAliasDeclaration.Import.AcceptVisitor(this, data)
			};
			
			imports.AddChild(clause, ImportsStatement.ImportsClauseRole);
			
			return EndNode(usingAliasDeclaration, imports);
		}
		
		public AstNode VisitUsingDeclaration(CSharp.UsingDeclaration usingDeclaration, object data)
		{
			var imports = new ImportsStatement();
			
			var clause = new MemberImportsClause() {
				Member = (AstType)usingDeclaration.Import.AcceptVisitor(this, data)
			};
			
			imports.AddChild(clause, ImportsStatement.ImportsClauseRole);
			
			return EndNode(usingDeclaration, imports);
		}
		
		public AstNode VisitExternAliasDeclaration(CSharp.ExternAliasDeclaration externAliasDeclaration, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitBlockStatement(CSharp.BlockStatement blockStatement, object data)
		{
			var block = new BlockStatement();
			blocks.Push(block);
			ConvertNodes(blockStatement, block.Statements);
			blocks.Pop();
			return EndNode(blockStatement, block);
		}
		
		public AstNode VisitBreakStatement(CSharp.BreakStatement breakStatement, object data)
		{
			var exit = new ExitStatement(ExitKind.None);
			
			foreach (var stmt in breakStatement.Ancestors) {
				if (stmt is CSharp.MethodDeclaration) {
					exit.ExitKind = IsSub(((CSharp.MethodDeclaration)stmt).ReturnType) ? ExitKind.Sub : ExitKind.Function;
					break;
				}
				if (stmt is CSharp.PropertyDeclaration) {
					exit.ExitKind = ExitKind.Property;
					break;
				}
				if (stmt is CSharp.DoWhileStatement) {
					exit.ExitKind = ExitKind.Do;
					break;
				}
				if (stmt is CSharp.ForStatement || stmt is CSharp.ForeachStatement) {
					exit.ExitKind = ExitKind.For;
					break;
				}
				if (stmt is CSharp.WhileStatement) {
					exit.ExitKind = ExitKind.While;
					break;
				}
				if (stmt is CSharp.SwitchStatement) {
					exit.ExitKind = ExitKind.Select;
					break;
				}
				if (stmt is CSharp.TryCatchStatement) {
					exit.ExitKind = ExitKind.Try;
					break;
				}
			}
			
			return EndNode(breakStatement, exit);
		}
		
		public AstNode VisitCheckedStatement(CSharp.CheckedStatement checkedStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitContinueStatement(CSharp.ContinueStatement continueStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitDoWhileStatement(CSharp.DoWhileStatement doWhileStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitEmptyStatement(CSharp.EmptyStatement emptyStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitExpressionStatement(CSharp.ExpressionStatement expressionStatement, object data)
		{
			var expr = new ExpressionStatement((Expression)expressionStatement.Expression.AcceptVisitor(this, data));
			return EndNode(expressionStatement, expr);
		}
		
		public AstNode VisitFixedStatement(CSharp.FixedStatement fixedStatement, object data)
		{
			// see http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.gchandle%28v=VS.100%29.aspx
			throw new NotImplementedException();
		}
		
		public AstNode VisitForeachStatement(CSharp.ForeachStatement foreachStatement, object data)
		{
			var stmt = new ForEachStatement() {
				Body = (BlockStatement)foreachStatement.EmbeddedStatement.AcceptVisitor(this, data),
				InExpression = (Expression)foreachStatement.InExpression.AcceptVisitor(this, data),
				Variable = new VariableInitializer() { Identifier = new VariableIdentifier() { Name = foreachStatement.VariableName }, Type = (AstType)foreachStatement.VariableType.AcceptVisitor(this, data) }
			};
			
			return EndNode(foreachStatement, stmt);
		}
		
		public AstNode VisitForStatement(CSharp.ForStatement forStatement, object data)
		{
			// for (;;) ;
			if (!forStatement.Initializers.Any() && forStatement.Condition.IsNull && !forStatement.Iterators.Any())
				return EndNode(forStatement, new WhileStatement() { Condition = new PrimitiveExpression(true), Body = (BlockStatement)forStatement.EmbeddedStatement.AcceptVisitor(this, data) });
			
			var stmt = new WhileStatement() {
				Condition = (Expression)forStatement.Condition.AcceptVisitor(this, data),
				Body = (BlockStatement)forStatement.EmbeddedStatement.AcceptVisitor(this, data)
			};
			ConvertNodes(forStatement.Iterators, stmt.Body.Statements);
			foreach (var initializer in forStatement.Initializers)
				blocks.Peek().Statements.Add((Statement)initializer.AcceptVisitor(this, data));
			
			return EndNode(forStatement, stmt);
		}
		
		public AstNode VisitGotoCaseStatement(CSharp.GotoCaseStatement gotoCaseStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitGotoDefaultStatement(CSharp.GotoDefaultStatement gotoDefaultStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitGotoStatement(CSharp.GotoStatement gotoStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitIfElseStatement(CSharp.IfElseStatement ifElseStatement, object data)
		{
			var stmt = new IfElseStatement();
			
			stmt.Condition = (Expression)ifElseStatement.Condition.AcceptVisitor(this, data);
			stmt.Body = (Statement)ifElseStatement.TrueStatement.AcceptVisitor(this, data);
			stmt.ElseBlock = (Statement)ifElseStatement.FalseStatement.AcceptVisitor(this, data);
			
			return EndNode(ifElseStatement, stmt);
		}
		
		public AstNode VisitLabelStatement(CSharp.LabelStatement labelStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitLockStatement(CSharp.LockStatement lockStatement, object data)
		{
			var stmt = new SyncLockStatement();
			
			stmt.Expression = (Expression)lockStatement.Expression.AcceptVisitor(this, data);
			stmt.Body = (BlockStatement)lockStatement.EmbeddedStatement.AcceptVisitor(this, data);
			
			return EndNode(lockStatement, stmt);
		}
		
		public AstNode VisitReturnStatement(CSharp.ReturnStatement returnStatement, object data)
		{
			var stmt = new ReturnStatement((Expression)returnStatement.Expression.AcceptVisitor(this, data));
			
			return EndNode(returnStatement, stmt);
		}
		
		public AstNode VisitSwitchStatement(CSharp.SwitchStatement switchStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitSwitchSection(CSharp.SwitchSection switchSection, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitCaseLabel(CSharp.CaseLabel caseLabel, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitThrowStatement(CSharp.ThrowStatement throwStatement, object data)
		{
			return EndNode(throwStatement, new ThrowStatement((Expression)throwStatement.Expression.AcceptVisitor(this, data)));
		}
		
		public AstNode VisitTryCatchStatement(CSharp.TryCatchStatement tryCatchStatement, object data)
		{
			var stmt = new TryStatement();
			
			stmt.Body = (BlockStatement)tryCatchStatement.TryBlock.AcceptVisitor(this, data);
			stmt.FinallyBlock = (BlockStatement)tryCatchStatement.FinallyBlock.AcceptVisitor(this, data);
			ConvertNodes(tryCatchStatement.CatchClauses, stmt.CatchBlocks);
			
			return EndNode(tryCatchStatement, stmt);
		}
		
		public AstNode VisitCatchClause(CSharp.CatchClause catchClause, object data)
		{
			var clause = new CatchBlock();
			
			clause.ExceptionType = (AstType)catchClause.Type.AcceptVisitor(this, data);
			clause.ExceptionVariable = catchClause.VariableName;
			ConvertNodes(catchClause.Body.Statements, clause.Statements);
			
			return EndNode(catchClause, clause);
		}
		
		public AstNode VisitUncheckedStatement(CSharp.UncheckedStatement uncheckedStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitUnsafeStatement(CSharp.UnsafeStatement unsafeStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitUsingStatement(CSharp.UsingStatement usingStatement, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitVariableDeclarationStatement(CSharp.VariableDeclarationStatement variableDeclarationStatement, object data)
		{
			var decl = new LocalDeclarationStatement();
			decl.Modifiers = Modifiers.Dim;
			ConvertNodes(variableDeclarationStatement.Variables, decl.Variables);
			
			return EndNode(variableDeclarationStatement, decl);
		}
		
		public AstNode VisitWhileStatement(CSharp.WhileStatement whileStatement, object data)
		{
			var stmt = new WhileStatement() {
				Condition = (Expression)whileStatement.Condition.AcceptVisitor(this, data),
				Body = (BlockStatement)whileStatement.EmbeddedStatement.AcceptVisitor(this, data)
			};
			
			return EndNode(whileStatement, stmt);
		}
		
		public AstNode VisitYieldBreakStatement(CSharp.YieldBreakStatement yieldBreakStatement, object data)
		{
			inIterator = true;
			return EndNode(yieldBreakStatement, new ReturnStatement());
		}
		
		public AstNode VisitYieldStatement(CSharp.YieldStatement yieldStatement, object data)
		{
			inIterator = true;
			return EndNode(yieldStatement, new YieldStatement((Expression)yieldStatement.Expression.AcceptVisitor(this, data)));
		}
		
		public AstNode VisitAccessor(CSharp.Accessor accessor, object data)
		{
			var result = new Accessor();
			
			ConvertNodes(accessor.Attributes, result.Attributes);
			ConvertNodes(accessor.ModifierTokens, result.ModifierTokens);
			result.Body = (BlockStatement)accessor.Body.AcceptVisitor(this, data);
			
			return EndNode(accessor, result);
		}
		
		public AstNode VisitConstructorDeclaration(CSharp.ConstructorDeclaration constructorDeclaration, object data)
		{
			var result = new ConstructorDeclaration();
			
			ConvertNodes(constructorDeclaration.Attributes, result.Attributes);
			ConvertNodes(constructorDeclaration.ModifierTokens, result.ModifierTokens);
			ConvertNodes(constructorDeclaration.Parameters, result.Parameters);
			result.Body = (BlockStatement)constructorDeclaration.Body.AcceptVisitor(this, data);
			if (!constructorDeclaration.Initializer.IsNull)
				result.Body.Statements.InsertBefore(result.Body.FirstOrDefault(), (Statement)constructorDeclaration.Initializer.AcceptVisitor(this, data));
			
			return EndNode(constructorDeclaration, result);
		}
		
		public AstNode VisitConstructorInitializer(CSharp.ConstructorInitializer constructorInitializer, object data)
		{
			var result = new InvocationExpression(
				new MemberAccessExpression() {
					Target = new InstanceExpression(constructorInitializer.ConstructorInitializerType == CSharp.ConstructorInitializerType.This ? InstanceExpressionType.Me : InstanceExpressionType.MyBase, AstLocation.Empty),
					Member = new Identifier("New", AstLocation.Empty)
				}
			);
			ConvertNodes(constructorInitializer.Arguments, result.Arguments);
			
			return EndNode(constructorInitializer, new ExpressionStatement(result));
		}
		
		public AstNode VisitDestructorDeclaration(CSharp.DestructorDeclaration destructorDeclaration, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitEnumMemberDeclaration(CSharp.EnumMemberDeclaration enumMemberDeclaration, object data)
		{
			var result = new EnumMemberDeclaration();
			
			ConvertNodes(enumMemberDeclaration.Attributes, result.Attributes);
			result.Name = new Identifier(enumMemberDeclaration.Name, AstLocation.Empty);
			result.Value = (Expression)enumMemberDeclaration.Initializer.AcceptVisitor(this, data);
			
			return EndNode(enumMemberDeclaration, result);
		}
		
		public AstNode VisitEventDeclaration(CSharp.EventDeclaration eventDeclaration, object data)
		{
			var result = new EventDeclaration();
			// TODO event declaration
			
//			ConvertNodes(eventDeclaration.Attributes, result.Attributes);
//			result.Modifiers = ConvertModifiers(eventDeclaration.Modifiers, eventDeclaration);
//			result.Name = new Identifier(eventDeclaration., AstLocation.Empty);
			
			return EndNode(eventDeclaration, result);
		}
		
		public AstNode VisitCustomEventDeclaration(CSharp.CustomEventDeclaration customEventDeclaration, object data)
		{
			var result = new EventDeclaration();
			ConvertNodes(customEventDeclaration.Attributes, result.Attributes);
			result.Modifiers = ConvertModifiers(customEventDeclaration.Modifiers, customEventDeclaration);
			result.IsCustom = true;
			result.Name = new Identifier(customEventDeclaration.Name, AstLocation.Empty);
			result.ReturnType = (AstType)customEventDeclaration.ReturnType.AcceptVisitor(this, data);
			if (!customEventDeclaration.PrivateImplementationType.IsNull)
				result.ImplementsClause.Add(
					new InterfaceMemberSpecifier((AstType)customEventDeclaration.PrivateImplementationType.AcceptVisitor(this, data), customEventDeclaration.Name));
			
			result.AddHandlerBlock = (Accessor)customEventDeclaration.AddAccessor.AcceptVisitor(this, data);
			result.RemoveHandlerBlock = (Accessor)customEventDeclaration.RemoveAccessor.AcceptVisitor(this, data);
			
			return EndNode(customEventDeclaration, result);
		}
		
		public AstNode VisitFieldDeclaration(CSharp.FieldDeclaration fieldDeclaration, object data)
		{
			var decl = new FieldDeclaration();
			
			ConvertNodes(fieldDeclaration.Attributes, decl.Attributes);
			decl.Modifiers = ConvertModifiers(fieldDeclaration.Modifiers, fieldDeclaration);
			ConvertNodes(fieldDeclaration.Variables, decl.Variables);
			
			return EndNode(fieldDeclaration, decl);
		}
		
		public AstNode VisitIndexerDeclaration(CSharp.IndexerDeclaration indexerDeclaration, object data)
		{
			var decl = new PropertyDeclaration();
			
			ConvertNodes(indexerDeclaration.Attributes.Where(section => section.AttributeTarget != "return"), decl.Attributes);
			decl.Getter = (Accessor)indexerDeclaration.Getter.AcceptVisitor(this, data);
			decl.Modifiers = ConvertModifiers(indexerDeclaration.Modifiers, indexerDeclaration);
			decl.Name = new Identifier(indexerDeclaration.Name, AstLocation.Empty);
			ConvertNodes(indexerDeclaration.Parameters, decl.Parameters);
			ConvertNodes(indexerDeclaration.Attributes.Where(section => section.AttributeTarget == "return"), decl.ReturnTypeAttributes);
			if (!indexerDeclaration.PrivateImplementationType.IsNull)
				decl.ImplementsClause.Add(
					new InterfaceMemberSpecifier((AstType)indexerDeclaration.PrivateImplementationType.AcceptVisitor(this, data),
					                             indexerDeclaration.Name));
			decl.ReturnType = (AstType)indexerDeclaration.ReturnType.AcceptVisitor(this, data);
			decl.Setter = (Accessor)indexerDeclaration.Setter.AcceptVisitor(this, data);
			
			if (!decl.Setter.IsNull) {
				decl.Setter.Parameters.Add(new ParameterDeclaration() {
				                           	Name = new Identifier("value", AstLocation.Empty),
				                           	Type = (AstType)indexerDeclaration.ReturnType.AcceptVisitor(this, data),
				                           });
			}
			
			return EndNode(indexerDeclaration, decl);
		}
		
		public AstNode VisitMethodDeclaration(CSharp.MethodDeclaration methodDeclaration, object data)
		{
			var result = new MethodDeclaration();
			
			ConvertNodes(methodDeclaration.Attributes.Where(section => section.AttributeTarget != "return"), result.Attributes);
			ConvertNodes(methodDeclaration.ModifierTokens, result.ModifierTokens);
			result.Name = new Identifier(methodDeclaration.Name, AstLocation.Empty);
			result.IsSub = IsSub(methodDeclaration.ReturnType);
			ConvertNodes(methodDeclaration.Parameters, result.Parameters);
			ConvertNodes(methodDeclaration.TypeParameters, result.TypeParameters);
			ConvertNodes(methodDeclaration.Attributes.Where(section => section.AttributeTarget == "return"), result.ReturnTypeAttributes);
			if (!methodDeclaration.PrivateImplementationType.IsNull)
				result.ImplementsClause.Add(
					new InterfaceMemberSpecifier((AstType)methodDeclaration.PrivateImplementationType.AcceptVisitor(this, data),
					                             methodDeclaration.Name));
			if (!result.IsSub)
				result.ReturnType = (AstType)methodDeclaration.ReturnType.AcceptVisitor(this, data);
			result.Body = (BlockStatement)methodDeclaration.Body.AcceptVisitor(this, data);
			
			if (inIterator) {
				result.Modifiers |= Modifiers.Iterator;
				inIterator = false;
			}
			
			return EndNode(methodDeclaration, result);
		}
		
		bool IsSub(CSharp.AstType returnType)
		{
			var t = returnType as CSharp.PrimitiveType;
			return t != null && t.Keyword == "void";
		}
		
		public AstNode VisitOperatorDeclaration(CSharp.OperatorDeclaration operatorDeclaration, object data)
		{
			var result = new OperatorDeclaration();
			ConvertNodes(operatorDeclaration.Attributes.Where(section => section.AttributeTarget != "return"), result.Attributes);
			ConvertNodes(operatorDeclaration.ModifierTokens, result.ModifierTokens);
			switch (operatorDeclaration.OperatorType) {
				case ICSharpCode.NRefactory.CSharp.OperatorType.LogicalNot:
				case ICSharpCode.NRefactory.CSharp.OperatorType.OnesComplement:
					result.Operator = OverloadableOperatorType.Not;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Increment:
				case ICSharpCode.NRefactory.CSharp.OperatorType.Decrement:
				case ICSharpCode.NRefactory.CSharp.OperatorType.True:
				case ICSharpCode.NRefactory.CSharp.OperatorType.False:
					throw new NotSupportedException();
				case ICSharpCode.NRefactory.CSharp.OperatorType.Implicit:
					result.Modifiers |= Modifiers.Widening;
					result.Operator = OverloadableOperatorType.CType;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Explicit:
					result.Modifiers |= Modifiers.Narrowing;
					result.Operator = OverloadableOperatorType.CType;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Addition:
					result.Operator = OverloadableOperatorType.Add;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Subtraction:
					result.Operator = OverloadableOperatorType.Subtract;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.UnaryPlus:
					result.Operator = OverloadableOperatorType.UnaryPlus;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.UnaryNegation:
					result.Operator = OverloadableOperatorType.UnaryMinus;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Multiply:
					result.Operator = OverloadableOperatorType.Multiply;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Division:
					result.Operator = OverloadableOperatorType.Divide;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Modulus:
					result.Operator = OverloadableOperatorType.Modulus;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.BitwiseAnd:
					result.Operator = OverloadableOperatorType.BitwiseAnd;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.BitwiseOr:
					result.Operator = OverloadableOperatorType.BitwiseOr;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.ExclusiveOr:
					result.Operator = OverloadableOperatorType.ExclusiveOr;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.LeftShift:
					result.Operator = OverloadableOperatorType.ShiftLeft;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.RightShift:
					result.Operator = OverloadableOperatorType.ShiftRight;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Equality:
					result.Operator = OverloadableOperatorType.Equality;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.Inequality:
					result.Operator = OverloadableOperatorType.InEquality;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.GreaterThan:
					result.Operator = OverloadableOperatorType.GreaterThan;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.LessThan:
					result.Operator = OverloadableOperatorType.LessThan;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.GreaterThanOrEqual:
					result.Operator = OverloadableOperatorType.GreaterThanOrEqual;
					break;
				case ICSharpCode.NRefactory.CSharp.OperatorType.LessThanOrEqual:
					result.Operator = OverloadableOperatorType.LessThanOrEqual;
					break;
				default:
					throw new Exception("Invalid value for OperatorType");
			}
			ConvertNodes(operatorDeclaration.Parameters, result.Parameters);
			ConvertNodes(operatorDeclaration.Attributes.Where(section => section.AttributeTarget == "return"), result.ReturnTypeAttributes);
			result.ReturnType = (AstType)operatorDeclaration.ReturnType.AcceptVisitor(this, data);
			result.Body = (BlockStatement)operatorDeclaration.Body.AcceptVisitor(this, data);
			
			return EndNode(operatorDeclaration, result);
		}
		
		public AstNode VisitParameterDeclaration(CSharp.ParameterDeclaration parameterDeclaration, object data)
		{
			var param = new ParameterDeclaration();
			
			ConvertNodes(parameterDeclaration.Attributes, param.Attributes);
			param.Modifiers = ConvertParamModifiers(parameterDeclaration.ParameterModifier);
			if ((param.Modifiers & Modifiers.None) == Modifiers.None)
				param.Modifiers = Modifiers.ByVal;
			if ((parameterDeclaration.ParameterModifier & ICSharpCode.NRefactory.CSharp.ParameterModifier.Out) == ICSharpCode.NRefactory.CSharp.ParameterModifier.Out) {
				AttributeBlock block = new AttributeBlock();
				block.Attributes.Add(new Ast.Attribute() { Type = new SimpleType("System.Runtime.InteropServices.OutAttribute") });
				param.Attributes.Add(block);
			}
			param.Name = new Identifier(parameterDeclaration.Name, AstLocation.Empty);
			param.Type = (AstType)parameterDeclaration.Type.AcceptVisitor(this, data);
			param.OptionalValue = (Expression)parameterDeclaration.DefaultExpression.AcceptVisitor(this, data);
			if (!param.OptionalValue.IsNull)
				param.Modifiers |= Modifiers.Optional;
			
			return EndNode(parameterDeclaration, param);
		}
		
		Modifiers ConvertParamModifiers(CSharp.ParameterModifier mods)
		{
			switch (mods) {
				case ICSharpCode.NRefactory.CSharp.ParameterModifier.None:
				case ICSharpCode.NRefactory.CSharp.ParameterModifier.This:
					return Modifiers.None;
				case ICSharpCode.NRefactory.CSharp.ParameterModifier.Ref:
					return Modifiers.ByRef;
				case ICSharpCode.NRefactory.CSharp.ParameterModifier.Out:
					return Modifiers.ByRef;
				case ICSharpCode.NRefactory.CSharp.ParameterModifier.Params:
					return Modifiers.ParamArray;
				default:
					throw new Exception("Invalid value for ParameterModifier");
			}
		}
		
		public AstNode VisitPropertyDeclaration(CSharp.PropertyDeclaration propertyDeclaration, object data)
		{
			var decl = new PropertyDeclaration();
			
			ConvertNodes(propertyDeclaration.Attributes.Where(section => section.AttributeTarget != "return"), decl.Attributes);
			decl.Getter = (Accessor)propertyDeclaration.Getter.AcceptVisitor(this, data);
			decl.Modifiers = ConvertModifiers(propertyDeclaration.Modifiers, propertyDeclaration);
			decl.Name = new Identifier(propertyDeclaration.Name, AstLocation.Empty);
			ConvertNodes(propertyDeclaration.Attributes.Where(section => section.AttributeTarget == "return"), decl.ReturnTypeAttributes);
			if (!propertyDeclaration.PrivateImplementationType.IsNull)
				decl.ImplementsClause.Add(
					new InterfaceMemberSpecifier((AstType)propertyDeclaration.PrivateImplementationType.AcceptVisitor(this, data),
					                             propertyDeclaration.Name));
			decl.ReturnType = (AstType)propertyDeclaration.ReturnType.AcceptVisitor(this, data);
			decl.Setter = (Accessor)propertyDeclaration.Setter.AcceptVisitor(this, data);
			
			if (!decl.Setter.IsNull) {
				decl.Setter.Parameters.Add(new ParameterDeclaration() {
				                           	Name = new Identifier("value", AstLocation.Empty),
				                           	Type = (AstType)propertyDeclaration.ReturnType.AcceptVisitor(this, data),
				                           });
			}
			
			if (inIterator) {
				decl.Modifiers |= Modifiers.Iterator;
				inIterator = false;
			}
			
			return EndNode(propertyDeclaration, decl);
		}
		
		public AstNode VisitVariableInitializer(CSharp.VariableInitializer variableInitializer, object data)
		{
			var decl = new VariableDeclaratorWithTypeAndInitializer();
			
			// look for type in parent
			decl.Type = (AstType)variableInitializer.Parent
				.GetChildByRole(CSharp.VariableInitializer.Roles.Type)
				.AcceptVisitor(this, data);
			decl.Identifiers.Add(new VariableIdentifier() { Name = variableInitializer.Name });
			decl.Initializer = (Expression)variableInitializer.Initializer.AcceptVisitor(this, data);
			
			return EndNode(variableInitializer, decl);
		}
		
		public AstNode VisitFixedFieldDeclaration(CSharp.FixedFieldDeclaration fixedFieldDeclaration, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitFixedVariableInitializer(CSharp.FixedVariableInitializer fixedVariableInitializer, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitCompilationUnit(CSharp.CompilationUnit compilationUnit, object data)
		{
			var unit = new CompilationUnit();

			foreach (var node in compilationUnit.Children)
				unit.AddChild(node.AcceptVisitor(this, null), CompilationUnit.MemberRole);
			
			return EndNode(compilationUnit, unit);
		}
		
		public AstNode VisitSimpleType(CSharp.SimpleType simpleType, object data)
		{
			var type = new SimpleType(simpleType.Identifier);
			ConvertNodes(simpleType.TypeArguments, type.TypeArguments);
			
			return EndNode(simpleType, type);
		}
		
		public AstNode VisitMemberType(CSharp.MemberType memberType, object data)
		{
			AstType target = null;
			
			if (memberType.Target is CSharp.SimpleType && ((CSharp.SimpleType)(memberType.Target)).Identifier.Equals("global", StringComparison.Ordinal))
				target = new PrimitiveType("Global");
			else
				target = (AstType)memberType.Target.AcceptVisitor(this, data);
			
			var type = new QualifiedType(target, new Identifier(memberType.MemberName, AstLocation.Empty));
			ConvertNodes(memberType.TypeArguments, type.TypeArguments);
			
			return EndNode(memberType, type);
		}
		
		public AstNode VisitComposedType(CSharp.ComposedType composedType, object data)
		{
			var type = new ComposedType();
			
			ConvertNodes(composedType.ArraySpecifiers, type.ArraySpecifiers);
			type.BaseType = (AstType)composedType.BaseType.AcceptVisitor(this, data);
			type.HasNullableSpecifier = composedType.HasNullableSpecifier;
			
			return EndNode(composedType, type);
		}
		
		public AstNode VisitArraySpecifier(CSharp.ArraySpecifier arraySpecifier, object data)
		{
			return EndNode(arraySpecifier, new ArraySpecifier(arraySpecifier.Dimensions));
		}
		
		public AstNode VisitPrimitiveType(CSharp.PrimitiveType primitiveType, object data)
		{
			string typeName;
			
			switch (primitiveType.Keyword) {
				case "object":
					typeName = "Object";
					break;
				case "bool":
					typeName = "Boolean";
					break;
				case "char":
					typeName = "Char";
					break;
				case "sbyte":
					typeName = "SByte";
					break;
				case "byte":
					typeName = "Byte";
					break;
				case "short":
					typeName = "Short";
					break;
				case "ushort":
					typeName = "UShort";
					break;
				case "int":
					typeName = "Integer";
					break;
				case "uint":
					typeName = "UInteger";
					break;
				case "long":
					typeName = "Long";
					break;
				case "ulong":
					typeName = "ULong";
					break;
				case "float":
					typeName = "Single";
					break;
				case "double":
					typeName = "Double";
					break;
				case "decimal":
					typeName = "Decimal";
					break;
				case "string":
					typeName = "String";
					break;
					// generic constraints
				case "new":
					typeName = "New";
					break;
				case "struct":
					typeName = "Structure";
					break;
				case "class":
					typeName = "Class";
					break;
				default:
					typeName = "unknown";
					break;
			}
			
			return EndNode(primitiveType, new PrimitiveType(typeName));
		}
		
		public AstNode VisitComment(CSharp.Comment comment, object data)
		{
			var c = new Comment(comment.Content, comment.CommentType == CSharp.CommentType.Documentation);
			
			if (comment.CommentType == CSharp.CommentType.MultiLine)
				throw new NotImplementedException();
			
			return EndNode(comment, c);
		}
		
		public AstNode VisitTypeParameterDeclaration(CSharp.TypeParameterDeclaration typeParameterDeclaration, object data)
		{
			var param = new TypeParameterDeclaration() {
				Variance = typeParameterDeclaration.Variance,
				Name = typeParameterDeclaration.Name
			};
			
			var constraint = typeParameterDeclaration.Parent
				.GetChildrenByRole(CSharp.AstNode.Roles.Constraint)
				.SingleOrDefault(c => c.TypeParameter == typeParameterDeclaration.Name);
			
			ConvertNodes(constraint == null ? Enumerable.Empty<CSharp.AstType>() : constraint.BaseTypes, param.Constraints);
			
			// TODO : typeParameterDeclaration.Attributes get lost?
			//ConvertNodes(typeParameterDeclaration.Attributes
			
			return EndNode(typeParameterDeclaration, param);
		}
		
		public AstNode VisitConstraint(CSharp.Constraint constraint, object data)
		{
			throw new NotImplementedException();
		}
		
		public AstNode VisitCSharpTokenNode(CSharp.CSharpTokenNode cSharpTokenNode, object data)
		{
			var mod = cSharpTokenNode as CSharp.CSharpModifierToken;
			if (mod != null) {
				var convertedModifiers = ConvertModifiers(mod.Modifier, mod.Parent);
				VBModifierToken token = null;
				if (convertedModifiers != Modifiers.None) {
					token = new VBModifierToken(AstLocation.Empty, convertedModifiers);
					return EndNode(cSharpTokenNode, token);
				}
				return EndNode(cSharpTokenNode, token);
			} else {
				throw new NotSupportedException("Should never visit individual tokens");
			}
		}
		
		Modifiers ConvertModifiers(CSharp.Modifiers modifier, CSharp.AstNode container)
		{
			if ((modifier & CSharp.Modifiers.Any) == CSharp.Modifiers.Any)
				return Modifiers.Any;
			
			var mod = Modifiers.None;
			
			if ((modifier & CSharp.Modifiers.Const) == CSharp.Modifiers.Const)
				mod |= Modifiers.Const;
			if ((modifier & CSharp.Modifiers.Abstract) == CSharp.Modifiers.Abstract) {
				if (container is CSharp.TypeDeclaration)
					mod |= Modifiers.MustInherit;
				else
					mod |= Modifiers.MustOverride;
			}
			if ((modifier & CSharp.Modifiers.Static) == CSharp.Modifiers.Static)
				mod |= Modifiers.Shared;
			
			if ((modifier & CSharp.Modifiers.Public) == CSharp.Modifiers.Public)
				mod |= Modifiers.Public;
			if ((modifier & CSharp.Modifiers.Protected) == CSharp.Modifiers.Protected)
				mod |= Modifiers.Protected;
			if ((modifier & CSharp.Modifiers.Internal) == CSharp.Modifiers.Internal)
				mod |= Modifiers.Friend;
			if ((modifier & CSharp.Modifiers.Private) == CSharp.Modifiers.Private)
				mod |= Modifiers.Private;
			if (container is CSharp.IndexerDeclaration)
				mod |= Modifiers.Default;
			bool writeable = IsWriteableProperty(container);
			bool readable = IsReadableProperty(container);
			if (writeable && !readable)
				mod |= Modifiers.WriteOnly;
			if (readable && !writeable)
				mod |= Modifiers.ReadOnly;
			
			
			return mod;
		}
		
		bool IsReadableProperty(ICSharpCode.NRefactory.CSharp.AstNode container)
		{
			if (container is CSharp.IndexerDeclaration) {
				var i = container as CSharp.IndexerDeclaration;
				return !i.Getter.IsNull;
			}
			
			if (container is CSharp.PropertyDeclaration) {
				var p = container as CSharp.PropertyDeclaration;
				return !p.Getter.IsNull;
			}
			
			return false;
		}
		
		bool IsWriteableProperty(ICSharpCode.NRefactory.CSharp.AstNode container)
		{
			if (container is CSharp.IndexerDeclaration) {
				var i = container as CSharp.IndexerDeclaration;
				return !i.Setter.IsNull;
			}
			
			if (container is CSharp.PropertyDeclaration) {
				var p = container as CSharp.PropertyDeclaration;
				return !p.Setter.IsNull;
			}
			
			return false;
		}
		
		public AstNode VisitIdentifier(CSharp.Identifier identifier, object data)
		{
			var ident = new Identifier(identifier.Name, ConvertLocation(identifier.StartLocation));
			
			return EndNode(identifier, ident);
		}
		
		public AstNode VisitPatternPlaceholder(CSharp.AstNode placeholder, ICSharpCode.NRefactory.PatternMatching.Pattern pattern, object data)
		{
			throw new NotImplementedException();
		}
		
		void ConvertNodes<T>(IEnumerable<CSharp.AstNode> nodes, VB.AstNodeCollection<T> result) where T : VB.AstNode
		{
			foreach (var node in nodes) {
				T n = (T)node.AcceptVisitor(this, null);
				if (n != null)
					result.Add(n);
			}
		}
		
		AstLocation ConvertLocation(CSharp.AstLocation location)
		{
			return new AstLocation(location.Line, location.Column);
		}
		
		T EndNode<T>(CSharp.AstNode node, T result) where T : VB.AstNode
		{
			return result;
		}
		
		bool HasAttribute(CSharp.AstNodeCollection<CSharp.AttributeSection> attributes, string name, out CSharp.Attribute foundAttribute)
		{
			foreach (var attr in attributes.SelectMany(a => a.Attributes)) {
				if (provider.GetTypeNameForAttribute(attr) == name) {
					foundAttribute = attr;
					return true;
				}
			}
			foundAttribute = null;
			return false;
		}
	}
}
