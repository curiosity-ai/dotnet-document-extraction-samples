---
title: Tensors in Ten Pages
subtitle: A gentle introduction to System.Numerics.Tensors
header: Tensors in Ten Pages
author: Luis Quintanilla
---

# Tensors in Ten Pages

A gentle introduction to System.Numerics.Tensors

Every modern AI model, from a tiny embedding model to a large language model, is built on
one simple data structure: the tensor. This short book shows you what tensors are, how
.NET stores them, and how to do useful math with them. You do not need any machine
learning background. If you have used a C# array, you are ready.

In this book:

- What is a tensor? Rank, shape, and the four shapes you will see most often.
- Shapes, strides, and memory: how a flat array becomes a multi-dimensional tensor.
- Doing math: element-wise operations, similarity, SoftMax, and matrix multiply.
- Where to go next: the full guide and the official documentation.

<div class="page-break"></div>

## What is a tensor?

A tensor is a multi-dimensional array of numbers. That is the whole idea. The rest of
this book is about how to describe those dimensions and how to compute with them.

You already know a few tensors, even if you never called them that:

- A single number, like a temperature reading, is a **scalar**.
- A list of numbers, like the red, green, and blue values of a color, is a **vector**.
- A grid of numbers, like a spreadsheet or a grayscale image, is a **matrix**.
- A stack of grids, like a color image with three channels, is a **3-D tensor**.

### Rank and shape

Two words describe every tensor. The **rank** is how many dimensions it has. The
**shape** lists the size of each dimension. A matrix with 2 rows and 3 columns has
rank 2 and shape [2, 3].

![Four tensors side by side: a scalar with shape [ ], a vector with shape [4], a matrix with shape [2, 3], and a 3-D tensor with shape [2, 3, 4]](figures/ranks.svg)
<p class="caption">Figure 1. Tensors of rank 0, 1, 2, and 3, labeled with their shapes.</p>

### Why AI uses tensors

Real data is naturally multi-dimensional. One word can be represented by 768 numbers
that capture its meaning (an **embedding**). A five-word sentence is then a [5, 768]
tensor. A batch of 32 sentences is a [32, 5, 768] tensor. Each layer of a neural network
takes in a tensor of one shape and produces a tensor of another shape.

### The .NET tensor types

The `System.Numerics.Tensors` NuGet package gives you a few core types:

- `Tensor<T>` owns its data and knows its shape. Use it for long-lived data like weights.
- `TensorSpan<T>` and `ReadOnlyTensorSpan<T>` are lightweight views over tensor data.
  They are ref structs, like `Span<T>`, so they live on the stack.
- `Tensor` is a static class with factory and helper methods such as `Tensor.Create`.
- `TensorPrimitives` is a static class of fast, SIMD-accelerated math over `Span<T>`.

<div class="callout"><strong>Note.</strong> In .NET 9, the <code>Tensor&lt;T&gt;</code>
family of types was marked <code>[Experimental]</code>, so you had to suppress a compiler
warning to use it. The guide this book is based on targets .NET 10, where these APIs no
longer need that suppression. <code>TensorPrimitives</code> has been stable since .NET 8.
If you use an older package version, expect the experimental warning.</div>

## Shapes, strides, and memory

The shape is what gives a tensor its meaning. The table below shows common shapes and
where you might meet them. Notice how quickly the element count grows: it is the product
of all the numbers in the shape.

| Name | Shape | Rank | Element count | Example |
|------|-------|------|---------------|---------|
| Scalar | [ ] | 0 | 1 | A single loss value |
| Vector | [768] | 1 | 768 | One word's embedding |
| Matrix | [5, 768] | 2 | 3,840 | A five-word sentence |
| 3-D tensor | [32, 5, 768] | 3 | 122,880 | A batch of 32 sentences |
| 4-D tensor | [32, 12, 5, 64] | 4 | 122,880 | Multi-head attention |

### Creating and inspecting a tensor

You create a tensor by handing over flat data and a shape. The shape tells .NET how to
interpret the numbers. The same six numbers can be a [2, 3] matrix or a [6] vector.

```csharp
using System.Numerics.Tensors;

var t = Tensor.Create<float>([1, 2, 3, 4, 5, 6], [2, 3]);
// [[1, 2, 3],
//  [4, 5, 6]]

Console.WriteLine(t.Rank);            // 2
Console.WriteLine(t.FlattenedLength); // 6
Console.WriteLine(t[1, 2]);           // 6

var flat = Tensor.Reshape(t, [6]);    // same data, shape [6]
var row1 = t.Slice(1..2, ..);         // a view, shape [1, 3]
```

The `Lengths` property returns the shape as a `ReadOnlySpan<nint>`. `Rank` is the number
of dimensions, and `FlattenedLength` is the total number of elements.

### Strides: from coordinates to memory

In memory, a tensor is just one flat array. .NET uses **row-major** order, which means
the last dimension changes fastest. **Strides** tell you how many elements to skip to
take one step along each dimension.

```text
shape    [2, 3, 4]
strides  [12, 4, 1]
element  [1, 2, 3]  ->  1*12 + 2*4 + 3*1 = 23
```

Here is why strides matter. When you slice or reshape, .NET can often change only the
shape and strides and leave the data where it is. That is why `Slice` returns a view
instead of a copy, and why reshaping a large tensor can be nearly free.

<div class="page-break"></div>

## Doing math

You can do tensor math at three levels. `Tensor<T>` operators like `a + b` are the most
readable. Static methods like `Tensor.Add(a, b)` give you explicit control.
`TensorPrimitives` works directly on spans and is the fastest path.

### Element-wise math with TensorPrimitives

`TensorPrimitives` methods take spans in and write results to a destination span. They
use your CPU's SIMD instructions automatically, so you get speed without extra work.

```csharp
float[] x = [1, 2, 3];
float[] y = [4, 5, 6];
float[] sum = new float[3];

TensorPrimitives.Add<float>(x, y, sum);         // [5, 7, 9]
float dot = TensorPrimitives.Dot<float>(x, y);  // 1*4 + 2*5 + 3*6 = 32
float cos = TensorPrimitives.CosineSimilarity<float>(x, y); // ~0.975

float[] logits = [2.0f, 1.0f, 0.1f];
float[] probs = new float[3];
TensorPrimitives.SoftMax<float>(logits, probs); // ~[0.659, 0.242, 0.099]
```

`SoftMax` turns raw scores (called **logits**) into probabilities that are all positive
and add up to 1. Language models use it to decide which token is most likely to come next.

### Measuring similarity

The **dot product** measures how much two vectors point in the same direction.
**Cosine similarity** divides the dot product by the length of each vector, so the
result is always between -1 and 1:

<p><strong>cosine similarity(a, b) = (a · b) / (|a| × |b|)</strong></p>

The length of a vector is |a| = √(a<sub>1</sub><sup>2</sup> + a<sub>2</sub><sup>2</sup> + ... +
a<sub>n</sub><sup>2</sup>). For x and y above, x · y = 32, |x| = √14 ≈ 3.742,
and |y| = √77 ≈ 8.775. So the similarity is 32 / 32.833 ≈ 0.975. A value close to 1 means
the vectors are very similar. This is the core of embedding search and RAG.

### Shapes in, shapes out

Every operation has rules about which shapes it accepts and which shape it returns.
**Broadcasting** lets a smaller tensor stretch to match a larger one. For example, you
can add a [1, 3] bias to every row of a [4, 3] matrix with `Tensor.Broadcast<float>`.

<table>
  <thead>
    <tr>
      <th rowspan="2">Operation</th>
      <th colspan="2">Input shapes</th>
      <th rowspan="2">Result shape</th>
    </tr>
    <tr>
      <th>A</th>
      <th>B</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Element-wise add</td>
      <td>[2, 2]</td>
      <td>[2, 2]</td>
      <td>[2, 2]</td>
    </tr>
    <tr>
      <td>Broadcasting add</td>
      <td>[4, 3]</td>
      <td>[1, 3]</td>
      <td>[4, 3]</td>
    </tr>
    <tr>
      <td>Dot product</td>
      <td>[3]</td>
      <td>[3]</td>
      <td>[ ] (a scalar)</td>
    </tr>
    <tr>
      <td>Matrix multiply</td>
      <td>[2, 3]</td>
      <td>[3, 2]</td>
      <td>[2, 2]</td>
    </tr>
  </tbody>
</table>

Broadcasting compares shapes from right to left. Two dimensions are compatible when they
are equal or when one of them is 1. So [4, 3] and [1, 3] work, but [4, 3] and [4] do not.

### Matrix multiply

Matrix multiply is built from dot products. Each cell of the result is the dot product of
one row of A with one column of B. For A with shape [2, 3] and B with shape [3, 2], the
inner sizes (3 and 3) must match, and the result has shape [2, 2].

![Matrix A [2, 3] times matrix B [3, 2] equals matrix C [2, 2], with 1×7 + 2×9 + 3×11 = 58 worked out](figures/matmul.svg)
<p class="caption">Figure 2. A worked [2, 3] × [3, 2] matrix multiply. Row 1 of A and column 1 of B give 58.</p>

You can check the other three cells yourself:

- Row 1, column 2: 1×8 + 2×10 + 3×12 = 8 + 20 + 36 = 64
- Row 2, column 1: 4×7 + 5×9 + 6×11 = 28 + 45 + 66 = 139
- Row 2, column 2: 4×8 + 5×10 + 6×12 = 32 + 50 + 72 = 154

Here is the same multiply in code. Storing B one column at a time makes every column a
contiguous span, so each cell is a single call to `TensorPrimitives.Dot`.

```csharp
float[] a = [1, 2, 3, 4, 5, 6];      // A, shape [2, 3]
float[] bT = [7, 9, 11, 8, 10, 12];  // B, shape [3, 2], stored by column
float[] c = new float[4];            // C, shape [2, 2]

for (int i = 0; i < 2; i++)
{
    for (int j = 0; j < 2; j++)
    {
        c[i * 2 + j] = TensorPrimitives.Dot(
            a.AsSpan(i * 3, 3), bT.AsSpan(j * 3, 3));
    }
}
// c = [58, 64, 139, 154]
```

<div class="callout"><strong>Tip.</strong> Let <code>Tensor&lt;T&gt;</code> manage shapes
and let <code>TensorPrimitives</code> do the math. To bridge the two, copy a tensor into a
flat array with <code>t.FlattenTo(buffer)</code>, then pass that array to any
<code>TensorPrimitives</code> method. If you created the tensor from an array you already
own, you can pass that array directly and skip the copy.</div>

## Where to go next

You now know the core ideas: rank and shape, strides and views, element-wise math,
similarity, and matrix multiply. These are the building blocks of every neural network
layer.

To keep going:

- Read the full guide, [.NET Tensors Guide](https://github.com/luisquintanilla/dotnet-tensors-guide),
  which builds from these basics to a complete language model inference engine. Its
  cookbook folder has small, runnable file-based apps for every topic.
- Browse the [System.Numerics.Tensors API reference on Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.numerics.tensors)
  for the full list of `Tensor` and `TensorPrimitives` methods.
- Install the package in your own project with `dotnet add package System.Numerics.Tensors`
  and try the examples from this book.
