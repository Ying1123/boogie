function F(int): bool;

procedure A()
{
    assume (forall {:inst_label "L"} x: int :: F(x));
    assert {:inst "L", 0} F(0);
    assert {:inst "L", 0} F(0);
    assert (forall {:inst "L", y+1} y: int :: F(y+1));
}

procedure B(j: int)
requires j > 0;
{
    var x: [int]bool;
    x := (lambda {:inst_label "M"} i: int :: if (i < j) then true else false);
    assert {:inst "M", 0} x[0];
}
