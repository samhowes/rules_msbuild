# //tests/Transitive

This package tests that Transitive dependencies work. The deps are:
`Binary --> Lib --> TransitiveLib --> TransitiveTransitiveLib`

The deepest library, `TransitiveTransitveLib` returns a value that is printed to the console by 
`Binary`. 

With the current build setup, the full closure of `Binary` should be copied to the output directory
of `Binary`.

todo(#17) collapse these directories.
