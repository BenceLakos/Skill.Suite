using Xunit;

// The event capture buffer and TestLogger's sink are process-wide static state; concurrent test
// classes would interleave into the same buffer.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
