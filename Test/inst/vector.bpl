// RUN: %boogie -lib "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

function {:inline} Vec_Concat<T>(v1: Vec T, v2: Vec T): Vec T {
    Vec(
        (lambda {:pool "Concat"} i: int ::
            if (i < 0) then Default()
            else if (0 <= i && i < Vec_Len(v1)) then Vec_Nth(v1, i)
            else if (Vec_Len(v1) <= i && i < Vec_Len(v1) + Vec_Len(v2)) then Vec_Nth(v2, i - Vec_Len(v1))
            else Default()),
        Vec_Len(v1) + Vec_Len(v2)
        )
}

function {:inline} Vec_Slice<T>(v: Vec T, i: int, j: int): Vec T {
    if (0 <= i && i < j && j <= len#Vec(v)) then
        Vec(
            (lambda {:pool "Slice"} k: int ::
                if (k < 0) then Default()
                else if (0 <= k && k < j - i) then Vec_Nth(v, k + i)
                else Default()),
            j - i
            )
    else Vec_Empty()
}

function {:inline} Vec_Swap<T>(v: Vec T, i: int, j: int): Vec T {
    if (0 <= i && i < len#Vec(v) && 0 <= j && j < len#Vec(v) && i != j)
    then Vec(contents#Vec(v)[i := contents#Vec(v)[j]][j := contents#Vec(v)[i]], len#Vec(v))
    else v
}

function {:inline} Vec_Remove<T>(v: Vec T): Vec T {
    if (0 < len#Vec(v))
    then Vec(contents#Vec(v)[len#Vec(v)-1 := Default()], len#Vec(v) - 1)
    else Vec_Empty()
}

type Element;

// extensionality lemma to be used explicitly by the programmer
procedure Vec_Ext(A: Vec Element, B: Vec Element) returns (i: int);
ensures A == B || Vec_Len(A) != Vec_Len(B) || Vec_Nth(A, i) != Vec_Nth(B, i);

// procedures Ex0 to Ex9 are exercises to ramp up to the "real" vector procedures
procedure Ex0(A: Vec Element, i: int)
requires 0 <= i && i < Vec_Len(A);
{
    assert Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i, Vec_Len(A))) == A;
}

procedure Ex1(A: Vec Element, i: int)
requires 0 <= i && i < Vec_Len(A) - 1;
requires Vec_Nth(A, i) == Vec_Nth(A, i + 1);
{
    assert
    Vec_Concat(Vec_Slice(A, 0, i + 1), Vec_Slice(A, i + 2, Vec_Len(A)))
    ==
    Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i + 1, Vec_Len(A)));

    assert Vec_Swap(A, i, i+1) == A;
}

procedure Ex2(A: Vec Element, i: int, j: int)
requires 0 <= i && i < Vec_Len(A);
requires 0 <= j && j < Vec_Len(A);
requires Vec_Nth(A, i) == Vec_Nth(A, j);
{
    assert Vec_Swap(A, i, j) == A;
}

procedure Ex3(A: Vec Element, i: int, j: int)
requires 0 <= i && i < Vec_Len(A);
requires 0 <= j && j < Vec_Len(A);
{
    assert
    Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i, Vec_Len(A)))
    ==
    Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j, Vec_Len(A)));
}

procedure Ex4(A: Vec Element, B: Vec Element, i: int, e: Element)
requires 0 <= i && i < Vec_Len(A);
requires Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i + 1, Vec_Len(A))) == B;
{
    var A', B': Vec Element;

    A' := Vec_Append(A, e);
    B' := Vec_Append(B, e);

    assert Vec_Concat(Vec_Slice(A', 0, i), Vec_Slice(A', i + 1, Vec_Len(A'))) == B';
}

procedure Ex5(A: Vec Element, B: Vec Element, i: int, e: Element)
requires 0 <= i && i < Vec_Len(A);
requires Vec_Nth(A, i) == Vec_Nth(B, Vec_Len(B) - 1);
requires Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i + 1, Vec_Len(A))) == Vec_Slice(B, 0, Vec_Len(B) - 1);
{
    var A', B': Vec Element;

    A' := Vec_Append(A, e);
    B' := Vec_Swap(Vec_Append(B, e), Vec_Len(B) - 1, Vec_Len(B));

    assert (forall x: int :: {:skolem_add_to_pool "Slice", x}
    Vec_Nth(Vec_Concat(Vec_Slice(A', 0, i), Vec_Slice(A', i + 1, Vec_Len(A'))), x) == Vec_Nth(Vec_Slice(B', 0, Vec_Len(B') - 1), x));
}

procedure Ex6a(A: Vec Element, B: Vec Element, i: int, e: Element)
requires 0 <= i && i < Vec_Len(A);
requires Vec_Nth(A, i) == Vec_Nth(B, Vec_Len(B) - 1);
requires Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i + 1, Vec_Len(A))) == Vec_Slice(B, 0, Vec_Len(B) - 1);
{
    var A', B': Vec Element;

    A' := Vec_Append(A, e);
    B' := Vec_Swap(Vec_Append(B, e), Vec_Len(B) - 1, Vec_Len(B));

    assert Vec_Nth(A', i) == Vec_Nth(B', Vec_Len(B') - 1);
}

procedure Ex6b(A: Vec Element, B: Vec Element, i: int, e: Element)
requires 0 <= i && i < Vec_Len(A);
requires Vec_Nth(A, i) == Vec_Nth(B, Vec_Len(B) - 1);
requires Vec_Concat(Vec_Slice(A, 0, i), Vec_Slice(A, i + 1, Vec_Len(A))) == Vec_Slice(B, 0, Vec_Len(B) - 1);
{
    var A', B': Vec Element;
    var x: int;

    A' := Vec_Append(A, e);
    B' := Vec_Swap(Vec_Append(B, e), Vec_Len(B) - 1, Vec_Len(B));

    call x := Vec_Ext(Vec_Concat(Vec_Slice(A', 0, i), Vec_Slice(A', i + 1, Vec_Len(A'))), Vec_Slice(B', 0, Vec_Len(B') - 1));
    assert {:add_to_pool "Slice", x}
    Vec_Concat(Vec_Slice(A', 0, i), Vec_Slice(A', i + 1, Vec_Len(A'))) == Vec_Slice(B', 0, Vec_Len(B') - 1);
}

procedure Ex7a(A: Vec Element, j: int, B: Vec Element, i: int)
requires 0 <= j && j <= i && i < Vec_Len(A) - 1;
requires Vec_Nth(B, i) == Vec_Nth(A, j);
requires Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
requires Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
{
    var B': Vec Element;
    var i': int;

    B' := Vec_Swap(B, i, i + 1);
    i' := i + 1;

    assert Vec_Nth(B', i') == Vec_Nth(A, j);
}

procedure Ex7b(A: Vec Element, j: int, B: Vec Element, i: int)
requires 0 <= j && j <= i && i < Vec_Len(A) - 1;
requires Vec_Nth(B, i) == Vec_Nth(A, j);
requires Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
requires Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
{
    var B': Vec Element;
    var i': int;
    var x: int;

    B' := Vec_Swap(B, i, i + 1);
    i' := i + 1;

    call x := Vec_Ext(Vec_Slice(B', 0, i'), Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i' + 1)));
    assert {:add_to_pool "Slice", 0, x + j, x - j, x, x + 1, x - 1}
    Vec_Slice(B', 0, i') == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i' + 1));
}

procedure Ex7c(A: Vec Element, j: int, B: Vec Element, i: int)
requires 0 <= j && j <= i && i < Vec_Len(A) - 1;
requires Vec_Nth(B, i) == Vec_Nth(A, j);
requires Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
requires Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
{
    var B': Vec Element;
    var i': int;
    var x: int;

    B' := Vec_Swap(B, i, i + 1);
    i' := i + 1;

    call x := Vec_Ext(Vec_Slice(B', i' + 1, Vec_Len(B')), Vec_Slice(A, i' + 1, Vec_Len(A)));
    assert {:add_to_pool "Slice", x, x + 1}
    Vec_Slice(B', i' + 1, Vec_Len(B')) == Vec_Slice(A, i' + 1, Vec_Len(A));
}

procedure Ex8(A: Vec Element, j: int, B: Vec Element, i: int)
returns (B': Vec Element, i': int)
requires 0 <= j && j <= i && i < Vec_Len(A) - 1;
requires Vec_Nth(B, i) == Vec_Nth(A, j);
requires Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
requires Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
ensures i' == i + 1;
ensures Vec_Nth(B', i') == Vec_Nth(A, j);
ensures Vec_Slice(B', 0, i') == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i' + 1));
ensures Vec_Slice(B', i' + 1, Vec_Len(B')) == Vec_Slice(A, i' + 1, Vec_Len(A));
{
    var x, y: int;

    B' := Vec_Swap(B, i, i + 1);
    i' := i + 1;

    assert Vec_Nth(B', i') == Vec_Nth(A, j);
    call x := Vec_Ext(Vec_Slice(B', 0, i'), Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i' + 1)));
    assert {:add_to_pool "Slice", 0, x + j, x - j, x, x + 1, x - 1}
    Vec_Slice(B', 0, i') == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i' + 1));
    call y := Vec_Ext(Vec_Slice(B', i' + 1, Vec_Len(B')), Vec_Slice(A, i' + 1, Vec_Len(A)));
    assert {:add_to_pool "Slice", y, y + 1}
    Vec_Slice(B', i' + 1, Vec_Len(B')) == Vec_Slice(A, i' + 1, Vec_Len(A));
}

procedure Ex9(A: Vec Element, j: int) returns (B: Vec Element, e: Element)
requires 0 <= j && j < Vec_Len(A);
ensures e == Vec_Nth(A, j);
ensures B == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, Vec_Len(A)));
{
    var i: int;
    var x, y, z: int;

    B := A;
    i := j;
    while (i < Vec_Len(A) - 1)
    invariant j <= i && i <= Vec_Len(A) - 1;
    invariant Vec_Nth(B, i) == Vec_Nth(A, j);
    invariant Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
    invariant Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
    {
        assert {:split_here} true;
        call B, i := Ex8(A, j, B, i);
    }
    e := Vec_Nth(B, Vec_Len(A) - 1);
    B := Vec_Remove(B);
    call z := Vec_Ext(B, Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, Vec_Len(A))));
    assume {:add_to_pool "Slice", z, j, j + 1, j - 1} true;
    assert {:split_here} true;
}

// "real" vector procedures start here
procedure remove(A: Vec Element, j: int) returns (B: Vec Element, e: Element)
requires 0 <= j && j < Vec_Len(A);
ensures e == Vec_Nth(A, j);
ensures B == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, Vec_Len(A)));
{
    var i: int;
    var x, y, z: int;

    B := A;
    i := j;
    while (i < Vec_Len(A) - 1)
    invariant j <= i && i <= Vec_Len(A) - 1;
    invariant Vec_Nth(B, i) == Vec_Nth(A, j);
    invariant Vec_Slice(B, 0, i) == Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1));
    invariant Vec_Slice(B, i + 1, Vec_Len(B)) == Vec_Slice(A, i + 1, Vec_Len(A));
    {
        assert {:split_here} true;
        assume {:add_to_pool "Slice", i, j} true;

        B := Vec_Swap(B, i, i + 1);
        i := i + 1;

        assume {:add_to_pool "Slice", i} true;
        call x := Vec_Ext(Vec_Slice(B, 0, i), Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, i + 1)));
        assume {:add_to_pool "Slice", 0, x + j, x - j, x, x + 1, x - 1} true;
        call y := Vec_Ext(Vec_Slice(B, i + 1, Vec_Len(B)), Vec_Slice(A, i + 1, Vec_Len(A)));
        assume {:add_to_pool "Slice", y, y + 1} true;
    }
    e := Vec_Nth(B, Vec_Len(A) - 1);
    B := Vec_Remove(B);
    call z := Vec_Ext(B, Vec_Concat(Vec_Slice(A, 0, j), Vec_Slice(A, j + 1, Vec_Len(A))));
    assume {:add_to_pool "Slice", z, j, j + 1, j - 1} true;
    assert {:split_here} true;
}

procedure swap_remove(A: Vec Element, j: int) returns (B: Vec Element)
requires 0 <= j && j < Vec_Len(A);
ensures Vec_Slice(B, 0, j) == Vec_Slice(A, 0, j);
ensures Vec_Slice(B, j+1, Vec_Len(B)) == Vec_Slice(A, j+1, Vec_Len(B));
ensures Vec_Nth(B, j) == Vec_Nth(A, Vec_Len(A) - 1);
{
    var last_idx: int;

    last_idx := Vec_Len(A) - 1;
    B := Vec_Swap(A, j, last_idx);
    B := Vec_Remove(B);
    assume {:add_to_pool "Slice", j, j + 1} true;
}

procedure reverse(A: Vec Element) returns (B: Vec Element)
ensures Vec_Len(A) == Vec_Len(B);
ensures (forall x: int :: 0 <= x && x < Vec_Len(A) ==> Vec_Nth(A, x) == Vec_Nth(B, Vec_Len(A) - 1 - x));
{
    var len: int;
    var front_index: int;
    var back_index: int;

    B := A;
    len := Vec_Len(A);
    if (len == 0) {
        return;
    }

    front_index := 0;
    back_index := len - 1;
    while (front_index < back_index)
    invariant front_index + back_index == len - 1;
    invariant Vec_Len(A) == Vec_Len(B);
    invariant (forall x: int :: 0 <= x && x < front_index ==> Vec_Nth(A, x) == Vec_Nth(B, Vec_Len(A) - 1 - x));
    invariant (forall x: int :: back_index < x && x < Vec_Len(A) ==> Vec_Nth(A, x) == Vec_Nth(B, Vec_Len(A) - 1 - x));
    invariant (forall x: int :: front_index <= x && x <= back_index ==> Vec_Nth(A, x) == Vec_Nth(B, x));
    {
        B := Vec_Swap(B, front_index, back_index);
        front_index := front_index + 1;
        back_index := back_index - 1;
    }
}

procedure append(A: Vec Element, B: Vec Element) returns (C: Vec Element)
ensures C == Vec_Concat(A, B);
{
    var R: Vec Element;
    var e: Element;
    var y: int;

    C := A;
    call R := reverse(B);
    while (0 < Vec_Len(R))
    invariant (forall {:pool "L"} x: int :: {:skolem_add_to_pool "L", 0, x + 1}
    0 <= x && x < Vec_Len(R) ==> Vec_Nth(B, x + Vec_Len(B) - Vec_Len(R)) == Vec_Nth(R, Vec_Len(R) - 1 - x));
    invariant C == Vec_Concat(A, Vec_Slice(B, 0, Vec_Len(B) - Vec_Len(R)));
    {
        e := Vec_Nth(R, Vec_Len(R) - 1);
        C := Vec_Append(C, e);
        R := Vec_Remove(R);
        assert Vec_Len(C) == Vec_Len(Vec_Concat(A, Vec_Slice(B, 0, Vec_Len(B) - Vec_Len(R))));
        call y := Vec_Ext(C, Vec_Concat(A, Vec_Slice(B, 0, Vec_Len(B) - Vec_Len(R))));
        assume {:add_to_pool "Slice", y, y - Vec_Len(A)} true;
    }
}

procedure contains(A: Vec Element, e: Element) returns (found: bool)
ensures !found <==> (forall x: int :: 0 <= x && x < Vec_Len(A) ==> Vec_Nth(A, x) != e);
{
    var i: int;
    var len: int;

    found := false;
    i := 0;
    len := Vec_Len(A);
    while (i < len)
    invariant 0 <= i;
    invariant (forall x: int :: 0 <= x && x < i ==> Vec_Nth(A, x) != e);
    {
        if (Vec_Nth(A, i) == e) {
            found := true;
            return;
        }
        i := i + 1;
    }
}

procedure index_of(A: Vec Element, e: Element) returns (found: bool, pos: int)
ensures found ==> Vec_Nth(A, pos) == e;
ensures !found ==> pos == 0 && (forall x: int :: 0 <= x && x < Vec_Len(A) ==> Vec_Nth(A, x) != e);
{
    var i: int;
    var len: int;

    found, pos := false, 0;
    i := 0;
    len := Vec_Len(A);
    while (i < len)
    invariant (forall x: int :: 0 <= x && x < i ==> Vec_Nth(A, x) != e);
    {
        if (Vec_Nth(A, i) == e) {
            found, pos := true, i;
            return;
        }
        i := i + 1;
    }
}
