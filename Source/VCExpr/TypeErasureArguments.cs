//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Basetypes;
using Microsoft.Boogie.VCExprAST;

// Erasure of types using explicit type parameters for functions

namespace Microsoft.Boogie.TypeErasure {
  using Microsoft.Boogie.VCExprAST;
  using HFNS = Microsoft.Boogie.VCExprAST.HelperFuns;

  public class TypeAxiomBuilderArguments : TypeAxiomBuilderIntBoolU {

    public TypeAxiomBuilderArguments(VCExpressionGenerator gen)
      : base(gen) {
      Contract.Requires(gen != null);

      Typed2UntypedFunctions = new Dictionary<Function/*!*/, Function/*!*/>();
    }

    // constructor to allow cloning
    [NotDelayed]
    internal TypeAxiomBuilderArguments(TypeAxiomBuilderArguments builder)
      : base(builder) {
      Contract.Requires(builder != null);
      Typed2UntypedFunctions =
        new Dictionary<Function/*!*/, Function/*!*/>(builder.Typed2UntypedFunctions);


      MapTypeAbstracterAttr =
        builder.MapTypeAbstracterAttr == null ?
        null : new MapTypeAbstractionBuilderArguments(this, builder.Gen,
                                                      builder.MapTypeAbstracterAttr);
    }

    public override Object Clone() {
      Contract.Ensures(Contract.Result<Object>() != null);
      return new TypeAxiomBuilderArguments(this);
    }

    ///////////////////////////////////////////////////////////////////////////////    

    // generate axioms of the kind "forall x:U. {Int2U(U2Int(x))} Int2U(U2Int(x))==x"
    // (this makes use of the assumption that only well-typed terms are generated
    // by the SMT-solver, i.e., that U2Int is only applied to terms that actually
    // are of type int)
    protected override VCExpr GenReverseCastAxiom(Function castToU, Function castFromU) {
      //Contract.Requires(castFromU != null);
      //Contract.Requires(castToU != null);
      Contract.Ensures(Contract.Result<VCExpr>() != null);
      List<VCTrigger/*!*/>/*!*/ triggers;
      VCExprVar/*!*/ var;
      VCExpr/*!*/ eq = GenReverseCastEq(castToU, castFromU, out var, out triggers);
      return Gen.Forall(HelperFuns.ToList(var), triggers, "cast:" + castFromU.Name, eq);
    }

    protected override VCExpr GenCastTypeAxioms(Function castToU, Function castFromU) {
      //Contract.Requires(castFromU != null);
      //Contract.Requires(castToU != null);
      Contract.Ensures(Contract.Result<VCExpr>() != null);
      // nothing
      return VCExpressionGenerator.True;
    }

    private MapTypeAbstractionBuilderArguments MapTypeAbstracterAttr = null;

    internal override MapTypeAbstractionBuilder/*!*/ MapTypeAbstracter {
      get {
        Contract.Ensures(Contract.Result<MapTypeAbstractionBuilder>() != null);

        if (MapTypeAbstracterAttr == null)
          MapTypeAbstracterAttr = new MapTypeAbstractionBuilderArguments(this, Gen);
        return MapTypeAbstracterAttr;
      }
    }

    protected override void AddVarTypeAxiom(VCExprVar var, Type originalType) {
      //Contract.Requires(originalType != null);
      //Contract.Requires(var != null);
      // no axioms are needed for variable or function types
    }

    ////////////////////////////////////////////////////////////////////////////
    // Symbols for representing functions

    // Globally defined functions
    private readonly IDictionary<Function/*!*/, Function/*!*/>/*!*/ Typed2UntypedFunctions;
    [ContractInvariantMethod]
    void Typed2UntypedFunctionsInvariantMethod() {
      Contract.Invariant(cce.NonNullElements(Typed2UntypedFunctions));
    }

    public Function Typed2Untyped(Function fun) {
      Contract.Requires(fun != null);
      Contract.Ensures(Contract.Result<Function>() != null);
      Function res;
      if (!Typed2UntypedFunctions.TryGetValue(fun, out res)) {
        Contract.Assert(fun.OutParams.Length == 1);

        // if all of the parameters are int or bool, the function does
        // not have to be changed
        if (Contract.ForAll(0, fun.InParams.Length, f => UnchangedType(cce.NonNull(fun.InParams[f]).TypedIdent.Type)) &&
            UnchangedType(cce.NonNull(fun.OutParams[0]).TypedIdent.Type)) {
          res = fun;
        } else {
          Type[]/*!*/ types = new Type[fun.TypeParameters.Length + fun.InParams.Length + 1];

          int i = 0;
          // the first arguments are the explicit type parameters
          for (int j = 0; j < fun.TypeParameters.Length; ++j) {
            types[i] = T;
            i = i + 1;
          }
          // followed by the actual parameters
          foreach (Variable/*!*/ x in fun.InParams) {
            Contract.Assert(x != null);
            types[i] = TypeAfterErasure(x.TypedIdent.Type);
            i = i + 1;
          }

          types[types.Length - 1] = TypeAfterErasure(cce.NonNull(fun.OutParams[0]).TypedIdent.Type);

          res = HelperFuns.BoogieFunction(fun.Name, types);
          res.Attributes = fun.Attributes;
        }

        Typed2UntypedFunctions.Add(fun, res);
      }
      return cce.NonNull(res);
    }

  }

  //////////////////////////////////////////////////////////////////////////////

  internal class MapTypeAbstractionBuilderArguments : MapTypeAbstractionBuilder {

    private readonly TypeAxiomBuilderArguments/*!*/ AxBuilderArguments;
    [ContractInvariantMethod]
    void ObjectInvariant() {
      Contract.Invariant(AxBuilderArguments != null);
    }


    internal MapTypeAbstractionBuilderArguments(TypeAxiomBuilderArguments axBuilder, VCExpressionGenerator gen)
      : base(axBuilder, gen) {
      Contract.Requires(gen != null);
      Contract.Requires(axBuilder != null);

      this.AxBuilderArguments = axBuilder;
    }

    // constructor for cloning
    internal MapTypeAbstractionBuilderArguments(TypeAxiomBuilderArguments axBuilder, VCExpressionGenerator gen, MapTypeAbstractionBuilderArguments builder)
      : base(axBuilder, gen, builder) {
      Contract.Requires(builder != null);
      Contract.Requires(gen != null);
      Contract.Requires(axBuilder != null);
      this.AxBuilderArguments = axBuilder;
    }

    ////////////////////////////////////////////////////////////////////////////

    protected override void GenSelectStoreFunctions(MapType abstractedType, TypeCtorDecl synonym, out Function/*!*/ select, out Function/*!*/ store) {
      //Contract.Requires(synonym != null);
//Contract.Requires(abstractedType != null);
Contract.Ensures(Contract.ValueAtReturn(out select) != null);
Contract.Ensures(Contract.ValueAtReturn(out store) != null);
      Contract.Assert(synonym.Name != null);
      string/*!*/ baseName = synonym.Name;
      int typeParamNum = abstractedType.FreeVariables.Length +
                         abstractedType.TypeParameters.Length;

      int arity = typeParamNum + abstractedType.Arguments.Length;

      Type/*!*/[]/*!*/ selectTypes = new Type/*!*/ [arity + 2];
      Type/*!*/[]/*!*/ storeTypes = new Type/*!*/ [arity + 3];

      int i = 0;
      // Fill in the free variables and type parameters
      for (; i < typeParamNum; i++) {
        selectTypes[i] = AxBuilder.T;
        storeTypes[i] = AxBuilder.T;
      }
      // Fill in the map type
      if (CommandLineOptions.Clo.MonomorphicArrays) {
        selectTypes[i] = abstractedType;
        storeTypes[i] = abstractedType;
      } else {
        selectTypes[i] = AxBuilder.U;
        storeTypes[i] = AxBuilder.U;
      }
      i++;
      // Fill in the index types
      foreach (Type/*!*/ type in abstractedType.Arguments) {
        Contract.Assert(type != null);
        if (CommandLineOptions.Clo.Monomorphize && AxBuilder.UnchangedType(type)) {
          selectTypes[i] = type;
          storeTypes[i] = type;
        } else {
          selectTypes[i] = AxBuilder.U;
          storeTypes[i] = AxBuilder.U;
        }
        i++;
      }
      // Fill in the output type for select function which also happens 
      // to be the type of the last argument to the store function
      if (CommandLineOptions.Clo.Monomorphize && AxBuilder.UnchangedType(abstractedType.Result)) {
        selectTypes[i] = abstractedType.Result;
        storeTypes[i] = abstractedType.Result;
      } else {
        selectTypes[i] = AxBuilder.U;
        storeTypes[i] = AxBuilder.U;
      }
      i++;
      // Fill in the map type which is the output of the store function
      if (CommandLineOptions.Clo.MonomorphicArrays)
        storeTypes[i] = abstractedType;
      else
        storeTypes[i] = AxBuilder.U;
      Contract.Assert(cce.NonNullElements<Type>(selectTypes));
      Contract.Assert(cce.NonNullElements<Type>(storeTypes));

      select = HelperFuns.BoogieFunction(baseName + "Select", selectTypes);
      store = HelperFuns.BoogieFunction(baseName + "Store", storeTypes);

      if (CommandLineOptions.Clo.UseArrayTheory) {
        select.AddAttribute("builtin", "select");
        store.AddAttribute("builtin", "store");
      } else {
        AxBuilder.AddTypeAxiom(GenMapAxiom0(select, store,
                 abstractedType.TypeParameters.Length, abstractedType.FreeVariables.Length));
        AxBuilder.AddTypeAxiom(GenMapAxiom1(select, store,
                 abstractedType.TypeParameters.Length, abstractedType.FreeVariables.Length));
      }
    }

    ///////////////////////////////////////////////////////////////////////////
    // The normal axioms of the theory of arrays (right now without extensionality)

    private VCExpr Select(Function select, List<VCExprVar/*!*/>/*!*/ types, VCExpr map, List<VCExprVar/*!*/>/*!*/ indexes) {
      Contract.Requires(map != null);
      Contract.Requires(select != null);
      Contract.Requires(cce.NonNullElements(indexes));
      Contract.Requires(cce.NonNullElements(types));
      Contract.Ensures(Contract.Result<VCExpr>() != null);
      List<VCExpr/*!*/>/*!*/ selectArgs = new List<VCExpr/*!*/>();
      selectArgs.AddRange(HelperFuns.ToVCExprList(types));
      selectArgs.Add(map);
      selectArgs.AddRange(HelperFuns.ToVCExprList(indexes));
      return Gen.Function(select, selectArgs);
    }

    private VCExpr Store(Function store, List<VCExprVar/*!*/>/*!*/ types, VCExpr map, List<VCExprVar/*!*/>/*!*/ indexes, VCExpr val) {
      Contract.Requires(val != null);
      Contract.Requires(map != null);
      Contract.Requires(store != null);
      Contract.Requires(cce.NonNullElements(indexes));
      Contract.Requires(cce.NonNullElements(types));
      Contract.Ensures(Contract.Result<VCExpr>() != null);
      List<VCExpr/*!*/>/*!*/ storeArgs = new List<VCExpr/*!*/>();
      storeArgs.AddRange(HelperFuns.ToVCExprList(types));
      storeArgs.Add(map);
      storeArgs.AddRange(HelperFuns.ToVCExprList(indexes));
      storeArgs.Add(val);
      return Gen.Function(store, storeArgs);
    }

    private VCExpr/*!*/ GenMapAxiom0(Function/*!*/ select, Function/*!*/ store,
      // bound type variables in the map type
                                 int mapTypeParamNum,
      // free type variables in the map
      // type (abstraction)
                                 int mapAbstractionVarNum) {
      Contract.Requires(select != null);
      Contract.Requires(store != null);
      Contract.Ensures(Contract.Result<VCExpr>() != null);

      int arity = select.InParams.Length - 1 - mapTypeParamNum - mapAbstractionVarNum;
      List<VCExprVar/*!*/>/*!*/ types =
        HelperFuns.VarVector("t", mapTypeParamNum + mapAbstractionVarNum,
                             AxBuilder.T, Gen);

      List<Type/*!*/> indexTypes = new List<Type/*!*/>();
      for (int i = mapTypeParamNum + mapAbstractionVarNum + 1; i < select.InParams.Length; i++) {
        indexTypes.Add(cce.NonNull(select.InParams[i]).TypedIdent.Type);
      }
      Contract.Assert(arity == indexTypes.Count);

      List<VCExprVar/*!*/>/*!*/ indexes = HelperFuns.VarVector("x", indexTypes, Gen);

      VCExprVar/*!*/ m = Gen.Variable("m", AxBuilder.U);
      Contract.Assert(m != null);
      VCExprVar/*!*/ val = Gen.Variable("val", cce.NonNull(select.OutParams[0]).TypedIdent.Type);
      Contract.Assert(val != null);

      VCExpr/*!*/ storeExpr = Store(store, types, m, indexes, val);
      Contract.Assert(storeExpr != null);
      VCExpr/*!*/ selectExpr = Select(select, types, storeExpr, indexes);
      Contract.Assert(selectExpr != null);

      List<VCExprVar/*!*/>/*!*/ quantifiedVars = new List<VCExprVar/*!*/>();
      quantifiedVars.AddRange(types);
      quantifiedVars.Add(val);
      quantifiedVars.Add(m);
      quantifiedVars.AddRange(indexes);

      VCExpr/*!*/ eq = Gen.Eq(selectExpr, val);
      Contract.Assert(eq != null);
      return Gen.Forall(quantifiedVars, new List<VCTrigger/*!*/>(),
                        "mapAx0:" + select.Name, eq);
    }

    private VCExpr/*!*/ GenMapAxiom1(Function/*!*/ select, Function/*!*/ store,
      // bound type variables in the map
      // type
                                 int mapTypeParamNum,
      // free type variables in the map
      // type (abstraction)
                                 int mapAbstractionVarNum) {
      Contract.Requires(select != null);
      Contract.Requires(store != null);
      Contract.Ensures(Contract.Result<VCExpr>() != null);

      int arity = select.InParams.Length - 1 - mapTypeParamNum - mapAbstractionVarNum;

      List<VCExprVar/*!*/>/*!*/ freeTypeVars =
        HelperFuns.VarVector("u", mapAbstractionVarNum, AxBuilder.T, Gen);
      List<VCExprVar/*!*/>/*!*/ boundTypeVars0 =
        HelperFuns.VarVector("s", mapTypeParamNum, AxBuilder.T, Gen);
      List<VCExprVar/*!*/>/*!*/ boundTypeVars1 =
        HelperFuns.VarVector("t", mapTypeParamNum, AxBuilder.T, Gen);

      List<VCExprVar/*!*/>/*!*/ types0 = new List<VCExprVar/*!*/>(boundTypeVars0);
      types0.AddRange(freeTypeVars);

      List<VCExprVar/*!*/>/*!*/ types1 = new List<VCExprVar/*!*/>(boundTypeVars1);
      types1.AddRange(freeTypeVars);

      List<Type/*!*/> indexTypes = new List<Type/*!*/>();
      for (int i = mapTypeParamNum + mapAbstractionVarNum + 1; i < select.InParams.Length; i++) {
        indexTypes.Add(cce.NonNull(select.InParams[i]).TypedIdent.Type);
      }
      Contract.Assert(arity == indexTypes.Count);

      List<VCExprVar/*!*/>/*!*/ indexes0 = HelperFuns.VarVector("x", indexTypes, Gen);
      List<VCExprVar/*!*/>/*!*/ indexes1 = HelperFuns.VarVector("y", indexTypes, Gen);

      VCExprVar/*!*/ m = Gen.Variable("m", AxBuilder.U);
      Contract.Assert(m != null);
      VCExprVar/*!*/ val = Gen.Variable("val", cce.NonNull(select.OutParams[0]).TypedIdent.Type);
      Contract.Assert(val != null);

      VCExpr/*!*/ storeExpr = Store(store, types0, m, indexes0, val);
      Contract.Assert(storeExpr != null);
      VCExpr/*!*/ selectWithoutStoreExpr = Select(select, types1, m, indexes1);
      Contract.Assert(selectWithoutStoreExpr != null);
      VCExpr/*!*/ selectExpr = Select(select, types1, storeExpr, indexes1);
      Contract.Assert(selectExpr != null);

      VCExpr/*!*/ selectEq = Gen.Eq(selectExpr, selectWithoutStoreExpr);
      Contract.Assert(selectEq != null);

      List<VCExprVar/*!*/>/*!*/ quantifiedVars = new List<VCExprVar/*!*/>();
      quantifiedVars.AddRange(freeTypeVars);
      quantifiedVars.AddRange(boundTypeVars0);
      quantifiedVars.AddRange(boundTypeVars1);
      quantifiedVars.Add(val);
      quantifiedVars.Add(m);
      quantifiedVars.AddRange(indexes0);
      quantifiedVars.AddRange(indexes1);

      List<VCTrigger/*!*/>/*!*/ triggers = new List<VCTrigger/*!*/>();

      // different value arguments or different type arguments are sufficient
      // to conclude that that value of the map at some point (after an update)
      // has not changed

      List<VCExpr/*!*/>/*!*/ indexEqs = new List<VCExpr/*!*/>();
      for (int i = 0; i < mapTypeParamNum; ++i)
        indexEqs.Add(Gen.Eq(boundTypeVars0[i], boundTypeVars1[i]));
      for (int i = 0; i < arity; ++i)
        indexEqs.Add(Gen.Eq(indexes0[i], indexes1[i]));

      VCExpr/*!*/ axiom = VCExpressionGenerator.True;
      int n = 0;
      foreach (VCExpr/*!*/ indexesEq in indexEqs) {
        Contract.Assert(indexesEq != null);
        VCExpr/*!*/ matrix = Gen.Or(indexesEq, selectEq);
        Contract.Assert(matrix != null);
        VCExpr/*!*/ conjunct = Gen.Forall(quantifiedVars, triggers,
                                      "mapAx1:" + select.Name + ":" + n, matrix);
        Contract.Assert(conjunct != null);
        axiom = Gen.AndSimp(axiom, conjunct);
        n = n + 1;
      }

      return axiom;
    }
  }

  //////////////////////////////////////////////////////////////////////////////

  public class TypeEraserArguments : TypeEraser {

    private readonly TypeAxiomBuilderArguments/*!*/ AxBuilderArguments;
    [ContractInvariantMethod]
    void ObjectInvariant() {
      Contract.Invariant(AxBuilderArguments != null);
    }


    private OpTypeEraser OpEraserAttr = null;
    protected override OpTypeEraser/*!*/ OpEraser {
      get {
        Contract.Ensures(Contract.Result<OpTypeEraser>() != null);

        if (OpEraserAttr == null)
          OpEraserAttr = new OpTypeEraserArguments(this, AxBuilderArguments, Gen);
        return OpEraserAttr;
      }
    }

    public TypeEraserArguments(TypeAxiomBuilderArguments axBuilder, VCExpressionGenerator gen) :base(axBuilder, gen){
      Contract.Requires(gen != null);
      Contract.Requires(axBuilder != null);
      
      this.AxBuilderArguments = axBuilder;
    }

    ////////////////////////////////////////////////////////////////////////////

    public override VCExpr Visit(VCExprQuantifier node, VariableBindings oldBindings) {
      Contract.Requires(oldBindings != null);
      Contract.Requires(node != null);
      Contract.Ensures(Contract.Result<VCExpr>() != null);
      VariableBindings/*!*/ bindings = oldBindings.Clone();

      // bound term variables are replaced with bound term variables
      // typed in a simpler way
      List<VCExprVar/*!*/>/*!*/ newBoundVars =
        BoundVarsAfterErasure(node.BoundVars, bindings);

      // type variables are replaced with ordinary quantified variables
      GenBoundVarsForTypeParams(node.TypeParameters, newBoundVars, bindings);
      VCExpr/*!*/ newNode = HandleQuantifier(node, newBoundVars, bindings);
      Contract.Assert(newNode != null);

      if (!(newNode is VCExprQuantifier) || !IsUniversalQuantifier(node))
        return newNode;

      VariableBindings/*!*/ bindings2;
      if (!RedoQuantifier(node, (VCExprQuantifier)newNode, node.BoundVars, oldBindings,
                          out bindings2, out newBoundVars))
        return newNode;

      GenBoundVarsForTypeParams(node.TypeParameters, newBoundVars, bindings2);
      return HandleQuantifier(node, newBoundVars, bindings2);
    }

    private void GenBoundVarsForTypeParams(List<TypeVariable/*!*/>/*!*/ typeParams, List<VCExprVar/*!*/>/*!*/ newBoundVars, VariableBindings bindings) {
      Contract.Requires(bindings != null);
      Contract.Requires(cce.NonNullElements(typeParams));
      Contract.Requires(cce.NonNullElements(newBoundVars));
      foreach (TypeVariable/*!*/ tvar in typeParams) {
        Contract.Assert(tvar != null);
        VCExprVar/*!*/ var = Gen.Variable(tvar.Name, AxBuilder.T);
        Contract.Assert(var != null);
        newBoundVars.Add(var);
        bindings.TypeVariableBindings.Add(tvar, var);
      }
    }

    private VCExpr HandleQuantifier(VCExprQuantifier node, List<VCExprVar/*!*/>/*!*/ newBoundVars, VariableBindings bindings){
Contract.Requires(bindings != null);
Contract.Requires(node != null);
Contract.Requires(cce.NonNullElements(newBoundVars));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      List<VCTrigger/*!*/>/*!*/ newTriggers = MutateTriggers(node.Triggers, bindings);
      Contract.Assert(cce.NonNullElements(newTriggers));
      VCExpr/*!*/ newBody = Mutate(node.Body, bindings);
      Contract.Assert(newBody != null);
      newBody = AxBuilder.Cast(newBody, Type.Bool);

      if (newBoundVars.Count == 0)  // might happen that no bound variables are left
        return newBody;
      return Gen.Quantify(node.Quan, new List<TypeVariable/*!*/>(), newBoundVars,
                          newTriggers, node.Infos, newBody);
    }
  }

  //////////////////////////////////////////////////////////////////////////////

  public class OpTypeEraserArguments : OpTypeEraser {

    protected readonly TypeAxiomBuilderArguments/*!*/ AxBuilderArguments;
    [ContractInvariantMethod]
    void ObjectInvariant() {
      Contract.Invariant(AxBuilderArguments != null);
    }


    public OpTypeEraserArguments(TypeEraserArguments eraser, TypeAxiomBuilderArguments axBuilder, VCExpressionGenerator gen) :base(eraser, axBuilder, gen){
      Contract.Requires(gen != null);
      Contract.Requires(axBuilder != null);
      Contract.Requires(eraser != null);
      this.AxBuilderArguments = axBuilder;
    }

    ////////////////////////////////////////////////////////////////////////////

    private VCExpr AssembleOpExpression(OpTypesPair opTypes, IEnumerable<VCExpr/*!*/>/*!*/ oldArgs, VariableBindings bindings){
Contract.Requires(bindings != null);
Contract.Requires(cce.NonNullElements(oldArgs));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      // UGLY: the code for tracking polarities should be factored out
      int oldPolarity = Eraser.Polarity;
      Eraser.Polarity = 0;

      List<VCExpr/*!*/>/*!*/ newArgs = new List<VCExpr/*!*/> ();
      // explicit type parameters
      foreach (Type/*!*/ t in opTypes.Types){
        Contract.Assert(newArgs != null);
        newArgs.Add(AxBuilder.Type2Term(t, bindings.TypeVariableBindings));}
      
      // and the actual value parameters
      Function/*!*/ newFun = ((VCExprBoogieFunctionOp)opTypes.Op).Func;
                              // ^ we only allow this operator at this point
      int i = opTypes.Types.Count;
      foreach (VCExpr/*!*/ arg in oldArgs) {
        Contract.Assert(arg != null);
        newArgs.Add(AxBuilder.Cast(Eraser.Mutate(arg, bindings),
                                   cce.NonNull(newFun.InParams[i]).TypedIdent.Type));
        i = i + 1;
      }

      Eraser.Polarity = oldPolarity;
      return Gen.Function(opTypes.Op, newArgs);
    }

    // for the time being, we store both the types of the arguments and the explicit
    // type parameters (for most operators, this is more than actually necessary)
    private OpTypesPair OriginalOpTypes(VCExprNAry node){
Contract.Requires(node != null);
      List<Type/*!*/>/*!*/ originalTypes = new List<Type/*!*/> ();
      foreach (VCExpr/*!*/ expr in node) {
        Contract.Assert(expr != null);
        originalTypes.Add(expr.Type);
      }
      originalTypes.AddRange(node.TypeArguments);
      return new OpTypesPair (node.Op, originalTypes);
    }

    private VCExpr EqualTypes(Type t0, Type t1, VariableBindings bindings){
Contract.Requires(bindings != null);
Contract.Requires(t1 != null);
Contract.Requires(t0 != null);
Contract.Ensures(Contract.Result<VCExpr>() != null);
      if (t0.Equals(t1))
        return VCExpressionGenerator.True;
      VCExpr/*!*/ t0Expr = AxBuilder.Type2Term(t0, bindings.TypeVariableBindings);
      Contract.Assert(t0Expr != null);
      VCExpr/*!*/ t1Expr = AxBuilder.Type2Term(t1, bindings.TypeVariableBindings);
      Contract.Assert(t1Expr != null);
      return Gen.Eq(t0Expr, t1Expr);
    }

    ///////////////////////////////////////////////////////////////////////////

    public override VCExpr VisitEqOp(VCExprNAry node, VariableBindings bindings) {
      Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      // we also have to state that the types are equal, because the
      // translation does not contain any information about the
      // relationship between values and types
      return Gen.AndSimp(base.VisitEqOp(node, bindings),
                         EqualTypes(node[0].Type, node[1].Type, bindings));
    }

    public override VCExpr VisitNeqOp(VCExprNAry node, VariableBindings bindings) {
      Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      // we also have to state that the types are (un)equal, because the
      // translation does not contain any information about the
      // relationship between values and types
      return Gen.OrSimp(base.VisitNeqOp(node, bindings),
                        Gen.Not(EqualTypes(node[0].Type, node[1].Type, bindings)));
    }

    public override VCExpr VisitSubtypeOp(VCExprNAry node, VariableBindings bindings) {
Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      // UGLY: the code for tracking polarities should be factored out
      int oldPolarity = Eraser.Polarity;
      Eraser.Polarity = 0;

      VCExpr/*!*/ res =
        Gen.Function(VCExpressionGenerator.Subtype3Op,
                     AxBuilder.Type2Term(node[0].Type,
                                         bindings.TypeVariableBindings),
                     AxBuilder.Cast(Eraser.Mutate(node[0], bindings),
                                    AxBuilder.U),
                     AxBuilder.Cast(Eraser.Mutate(node[1], bindings),
                                    AxBuilder.U));

      Eraser.Polarity = oldPolarity;
      return res;
    }

    public override VCExpr VisitSelectOp(VCExprNAry node, VariableBindings bindings) {
Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      OpTypesPair originalOpTypes = OriginalOpTypes(node);
      OpTypesPair newOpTypes;

      if (!NewOpCache.TryGetValue(originalOpTypes, out newOpTypes)) {
        MapType/*!*/ rawType = node[0].Type.AsMap;
        Contract.Assert(rawType != null);
        TypeSeq/*!*/ abstractionInstantiation;
        Function/*!*/ select =
          AxBuilder.MapTypeAbstracter.Select(rawType, out abstractionInstantiation);
        Contract.Assert(abstractionInstantiation != null);
        newOpTypes = TypesPairForSelectStore(node, select, abstractionInstantiation);
        NewOpCache.Add(originalOpTypes, newOpTypes);
      }

      return AssembleOpExpression(newOpTypes, node, bindings);
    }

    public override VCExpr VisitStoreOp(VCExprNAry node, VariableBindings bindings) {
Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      OpTypesPair originalOpTypes = OriginalOpTypes(node);
      OpTypesPair newOpTypes;

      if (!NewOpCache.TryGetValue(originalOpTypes, out newOpTypes)) {
        MapType/*!*/ rawType = node[0].Type.AsMap;
        TypeSeq/*!*/ abstractionInstantiation;
        Function/*!*/ store =
          AxBuilder.MapTypeAbstracter.Store(rawType, out abstractionInstantiation);

        newOpTypes = TypesPairForSelectStore(node, store, abstractionInstantiation);
        NewOpCache.Add(originalOpTypes, newOpTypes);
      }

      return AssembleOpExpression(newOpTypes, node, bindings);
    }

    private OpTypesPair TypesPairForSelectStore(VCExprNAry/*!*/ node, Function/*!*/ untypedOp,
      // instantiation of the abstract map type parameters
                                                TypeSeq/*!*/ abstractionInstantiation) {
      Contract.Requires(node != null);
      Contract.Requires(untypedOp != null);
      Contract.Requires(abstractionInstantiation != null);

      List<Type/*!*/>/*!*/ inferredTypeArgs = new List<Type/*!*/> ();
      foreach (Type/*!*/ t in node.TypeArguments){Contract.Assert(t != null);
//        inferredTypeArgs.Add(AxBuilder.MapTypeAbstracter.AbstractMapTypeRecursively(t));
        inferredTypeArgs.Add(t);}
      foreach (Type/*!*/ t in abstractionInstantiation) {
        Contract.Assert(t != null);
        inferredTypeArgs.Add(t);}

      Contract.Assert(untypedOp.InParams.Length == inferredTypeArgs.Count + node.Arity);
      return new OpTypesPair (Gen.BoogieFunctionOp(untypedOp), inferredTypeArgs);
    }

    ///////////////////////////////////////////////////////////////////////////

    public override VCExpr VisitBoogieFunctionOp(VCExprNAry node, VariableBindings bindings) {
Contract.Requires((bindings != null));
Contract.Requires((node != null));
Contract.Ensures(Contract.Result<VCExpr>() != null);
      OpTypesPair originalOpTypes = OriginalOpTypes(node);
      OpTypesPair newOpTypes;

      if (!NewOpCache.TryGetValue(originalOpTypes, out newOpTypes)) {
        Function/*!*/ oriFun = ((VCExprBoogieFunctionOp)node.Op).Func;
        Contract.Assert(oriFun != null);
        List<Type/*!*/>/*!*/ inferredTypeArgs = new List<Type/*!*/> ();
        foreach (Type/*!*/ t in node.TypeArguments){Contract.Assert(t != null);
//          inferredTypeArgs.Add(AxBuilder.MapTypeAbstracter.AbstractMapTypeRecursively(t));
          inferredTypeArgs.Add(t);}

        VCExprOp/*!*/ newOp = Gen.BoogieFunctionOp(AxBuilderArguments.Typed2Untyped(oriFun));
        newOpTypes = new OpTypesPair (newOp, inferredTypeArgs);

        NewOpCache.Add(originalOpTypes, newOpTypes);
      }

      return AssembleOpExpression(newOpTypes, node, bindings);
    }

    ///////////////////////////////////////////////////////////////////////////

    // cache from the typed operators to the untyped operators with
    // explicit type arguments. the keys are pairs of the typed
    // operator and the actual types of the argument expressions, the
    // values are pairs of the new operators and the types that have
    // to be given as explicit type arguments
    private readonly IDictionary<OpTypesPair, OpTypesPair>/*!*/ NewOpCache =
      new Dictionary<OpTypesPair, OpTypesPair>();

    private struct OpTypesPair {
      public readonly VCExprOp/*!*/ Op;
      public readonly List<Type/*!*/>/*!*/ Types;
      [ContractInvariantMethod]
      void ObjectInvariant() {
        Contract.Invariant(Op != null);
        Contract.Invariant(cce.NonNullElements(Types));
      }


      public OpTypesPair(VCExprOp op, List<Type/*!*/>/*!*/ types) {
        Contract.Requires(op != null);
        Contract.Requires(cce.NonNullElements(types));
        this.Op = op;
        this.Types = types;
        this.HashCode = HFNS.PolyHash(op.GetHashCode(), 17, types);
      }

      [Pure]
      [Reads(ReadsAttribute.Reads.Nothing)]
      public override bool Equals(object that) {
        if (that is OpTypesPair) {
          OpTypesPair thatPair = (OpTypesPair)that;
          return this.Op.Equals(thatPair.Op) &&
            HFNS.SameElements(this.Types, thatPair.Types);
        }
        return false;
      }

      private readonly int HashCode;

      [Pure]
      public override int GetHashCode() {
        return HashCode;
      }
    }
  }
}