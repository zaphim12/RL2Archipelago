using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

/// <summary>
/// Draws the whole branch diagram as a single uGUI mesh: one draw call, and, crucially, geometry
/// that never overlaps itself.
///
/// <para>Drawing each line as its own quad running node-centre to node-centre produces 
/// artifacts this class exists to remove. Square caps left a notch on the outside of every bend,
/// and because every line ran all the way into the node centre, the lines stacked on top of each
/// other there.</para>
///
/// <para>The manor is laid out on a grid and every connection is purely horizontal or vertical,
/// which makes the fix simple. Each node owns a square of side <see cref="ManorBranchLayout.Thickness"/>
/// centred on it. Each of the square's four sides is either <em>open</em> (a line leaves that way,
/// and its strip starts where the square ends) or closed, in which case the side is just outline.
/// Nothing is drawn twice, so alpha is uniform everywhere.</para>
///
/// <para>Each of the square's four corners then falls into one of three cases, which is the whole
/// of the corner handling:</para>
/// <list type="bullet">
/// <item>both sides open: the crease where a fork splits, <em>filled in</em> by an arc tangent to
/// both lines. Left alone this crease is what makes a branch point look pinched.</item>
/// <item>both sides closed: a corner of the outline, <em>cut back</em> by that same arc. On an
/// elbow this is the outside of the bend; on a dead end, the two corners of the back edge meet and
/// the line simply ends in a rounded cap.</item>
/// <item>one of each: the closed side and the open side's edge are the same line, so the outline
/// runs straight through and there is nothing to round.</item>
/// </list>
///
/// <para>Color belongs to nodes rather than lines. The junction square is solidly its node's color
/// and each line is a gradient into its child's, confined to the first
/// <see cref="ManorBranchLayout.ColorBlendLength"/> units, so every line leaves a fork already
/// wearing the fork's color and grows into its own. This blend is what makes a junction between two
/// different colors read as a branch splitting.</para>
///
/// <para>Antialiasing is coverage-based: UV.y carries <c>distance-to-outline / thickness</c>,
/// sampled against <see cref="Ramp"/>.</para>
/// </summary>
internal sealed class ManorBranchMesh : MaskableGraphic
{
    /// <summary>A node of the tree. <see cref="Color"/> carries the revealed/dim state in its
    /// alpha, so re-dimming is just a color swap and a re-tessellation.</summary>
    internal struct Joint
    {
        public Vector2 Pos;
        public Color Color;
    }

    /// <summary>Points closer together than this are treated as the same vertex.</summary>
    private const float Epsilon = 0.01f;

    /// <summary>Segments per corner. Corners are always 90°, so this is the whole arc.</summary>
    private const int ArcSteps = 6;

    /// <summary>East, north, west, south. Side <c>d</c> of a node's square faces
    /// <c>Axis[d]</c>, and corner <c>d</c> is the one between side <c>d</c> and side
    /// <c>d + 1</c>.</summary>
    private static readonly Vector2[] Axis = { new(1f, 0f), new(0f, 1f), new(-1f, 0f), new(0f, -1f) };

    private readonly List<Joint> _joints = new();
    private readonly List<(int A, int B)> _links = new();

    /// <summary>Per joint, a bitmask of which of the four sides a line leaves by.</summary>
    private int[] _open = Array.Empty<int>();

    /// <summary>Per link, the side of its A node it leaves by. The B end is the opposite side.</summary>
    private int[] _side = Array.Empty<int>();

    private static Texture2D s_ramp;

    public override Texture mainTexture => Ramp;

    // ── Data ─────────────────────────────────────────────────────────────────

    /// <summary>Replaces the whole diagram. Positions must already be in this graphic's local
    /// space, and are expected to be grid-aligned: a link that is not purely horizontal or
    /// vertical is snapped to whichever axis it is closest to, and will meet its far node at an
    /// angle.</summary>
    public void SetTree(IList<Joint> joints, IList<(int A, int B)> links)
    {
        _joints.Clear();
        _joints.AddRange(joints);
        _links.Clear();
        _links.AddRange(links);

        _open = new int[_joints.Count];
        _side = new int[_links.Count];

        for (int l = 0; l < _links.Count; l++)
        {
            var (a, b) = _links[l];
            if (a == b || a < 0 || b < 0 || a >= _joints.Count || b >= _joints.Count)
            {
                _side[l] = -1;
                continue;
            }

            int side = SideTowards(_joints[b].Pos - _joints[a].Pos);
            _side[l] = side;
            _open[a] |= 1 << side;
            _open[b] |= 1 << Opposite(side);
        }

        SetVerticesDirty();
    }

    /// <summary>Recolors in place, keeping the cached layout. Alpha rides along in the color.</summary>
    public void SetJointColors(IList<Color> colors)
    {
        int count = Mathf.Min(colors.Count, _joints.Count);
        for (int i = 0; i < count; i++)
        {
            var joint = _joints[i];
            joint.Color = colors[i];
            _joints[i] = joint;
        }
        SetVerticesDirty();
    }

    private static int SideTowards(Vector2 delta) =>
        Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? (delta.x >= 0f ? 0 : 2)
            : (delta.y >= 0f ? 1 : 3);

    private static int Opposite(int side) => (side + 2) % 4;

    private bool IsOpen(int joint, int side) => (_open[joint] & (1 << side)) != 0;

    // ── Mesh generation ──────────────────────────────────────────────────────

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_side.Length != _links.Count) return;

        for (int j = 0; j < _joints.Count; j++) EmitJoint(vh, j);
        for (int l = 0; l < _links.Count; l++) EmitLink(vh, l);
    }

    /// <summary>The square at a node: its outline walked counter-clockwise, plus a separate wedge
    /// filling each crease between two lines that leave at right angles to each other.</summary>
    private void EmitJoint(VertexHelper vh, int joint)
    {
        float half = ManorBranchLayout.Thickness * 0.5f;
        float radius = Mathf.Min(ManorBranchLayout.CornerRounding, half);
        Vector2 centre = _joints[joint].Pos;
        Color color = _joints[joint].Color;

        var outline = new List<(Vector2 P, float V)>();

        for (int i = 0; i < 4; i++)
        {
            int nextSide = (i + 1) % 4;
            bool openA = IsOpen(joint, i), openB = IsOpen(joint, nextSide);
            Vector2 corner = centre + half * (Axis[i] + Axis[nextSide]);

            if (!openA && !openB && radius > Epsilon)
            {
                // Cut the corner back. Walking counter-clockwise we meet side i's edge first.
                AppendArc(outline, corner - radius * (Axis[i] + Axis[nextSide]), radius,
                          corner - radius * Axis[nextSide], corner - radius * Axis[i]);
            }
            else
            {
                outline.Add((corner, CornerCoverage(openA && openB, radius)));
            }

            // The midpoint of an open side is inside the shape (the strip continues through it)
            // so it has to carry full coverage, or the line would fade out down its own middle.
            if (IsOpen(joint, nextSide)) outline.Add((centre + half * Axis[nextSide], 0.5f));
        }

        EmitFan(vh, centre, 0.5f, outline, color);

        for (int i = 0; i < 4; i++)
        {
            int nextSide = (i + 1) % 4;
            if (!IsOpen(joint, i) || !IsOpen(joint, nextSide) || radius <= Epsilon) continue;

            // Fill the crease: an arc tangent to the facing edge of each of the two strips.
            Vector2 corner = centre + half * (Axis[i] + Axis[nextSide]);
            var fillet = new List<(Vector2 P, float V)>();
            AppendArc(fillet, corner + radius * (Axis[i] + Axis[nextSide]), radius,
                      corner + radius * Axis[i], corner + radius * Axis[nextSide]);

            EmitFan(vh, corner, CornerCoverage(true, radius), fillet, color);
        }
    }

    /// <summary>Coverage at a square's corner. A filled crease buries the corner under the fillet,
    /// so how solid it is comes from how far the fillet's arc stands off it.</summary>
    private static float CornerCoverage(bool crease, float radius) =>
        crease ? Mathf.Min(0.5f, radius * (Mathf.Sqrt(2f) - 1f) / ManorBranchLayout.Thickness) : 0f;

    /// <summary>The plain part of a line, spanning between the two nodes' squares. It is emitted as
    /// two quads either side of the centre line: the outer edges sit on the outline and so carry no
    /// coverage, leaving the centre vertices to hold the line solid.</summary>
    private void EmitLink(VertexHelper vh, int link)
    {
        int side = _side[link];
        if (side < 0) return;

        var (ia, ib) = _links[link];
        float half = ManorBranchLayout.Thickness * 0.5f;
        Vector2 a = _joints[ia].Pos;
        Vector2 u = Axis[side], n = Axis[(side + 1) % 4];

        float span = Vector2.Dot(_joints[ib].Pos - a, u);
        float near = half, far = span - half;
        if (far - near <= Epsilon) return;

        // Coverage at the four outer corners has to agree with whatever the node squares did with
        // the corners they share with this line, or the seam between them shows.
        float radius = Mathf.Min(ManorBranchLayout.CornerRounding, half);
        float nearHigh = JointCorner(ia, side, radius);
        float nearLow = JointCorner(ia, (side + 3) % 4, radius);
        int back = Opposite(side);
        float farLow = JointCorner(ib, back, radius);
        float farHigh = JointCorner(ib, (back + 3) % 4, radius);

        Color from = _joints[ia].Color, to = _joints[ib].Color;

        // A cross-section at the color-blend distance keeps the gradient where it belongs instead
        // of letting it stretch across the whole line.
        float blend = ManorBranchLayout.ColorBlendLength;
        if (blend > near + Epsilon && blend < far - Epsilon)
        {
            EmitStrip(vh, a, u, n, half, near, blend, nearLow, nearHigh, 0f, 0f, from, to);
            EmitStrip(vh, a, u, n, half, blend, far, 0f, 0f, farLow, farHigh, from, to);
        }
        else
        {
            EmitStrip(vh, a, u, n, half, near, far, nearLow, nearHigh, farLow, farHigh, from, to);
        }
    }

    /// <summary>Coverage at one corner of a node's square, as the square itself drew it.</summary>
    private float JointCorner(int joint, int corner, float radius) =>
        CornerCoverage(IsOpen(joint, corner) && IsOpen(joint, (corner + 1) % 4), radius);

    private void EmitStrip(VertexHelper vh, Vector2 a, Vector2 u, Vector2 n, float half,
                           float s0, float s1, float lowV0, float highV0, float lowV1, float highV1,
                           Color from, Color to)
    {
        Vector2 p0 = a + s0 * u, p1 = a + s1 * u;
        Color c0 = BlendAt(s0, from, to), c1 = BlendAt(s1, from, to);

        int l0 = AddVert(vh, p0 - half * n, lowV0, c0);
        int m0 = AddVert(vh, p0, 0.5f, c0);
        int h0 = AddVert(vh, p0 + half * n, highV0, c0);
        int l1 = AddVert(vh, p1 - half * n, lowV1, c1);
        int m1 = AddVert(vh, p1, 0.5f, c1);
        int h1 = AddVert(vh, p1 + half * n, highV1, c1);

        vh.AddTriangle(l0, m0, m1);
        vh.AddTriangle(l0, m1, l1);
        vh.AddTriangle(m0, h0, h1);
        vh.AddTriangle(m0, h1, m1);
    }

    private void EmitFan(VertexHelper vh, Vector2 centre, float centreV,
                         List<(Vector2 P, float V)> loop, Color color)
    {
        Dedupe(loop);
        if (loop.Count > 1 && (loop[loop.Count - 1].P - loop[0].P).sqrMagnitude < Epsilon * Epsilon)
            loop.RemoveAt(loop.Count - 1);
        if (loop.Count < 3) return;

        int hub = AddVert(vh, centre, centreV, color);
        int first = AddVert(vh, loop[0].P, loop[0].V, color);
        int previous = first;

        for (int i = 1; i < loop.Count; i++)
        {
            int current = AddVert(vh, loop[i].P, loop[i].V, color);
            vh.AddTriangle(hub, previous, current);
            previous = current;
        }
        vh.AddTriangle(hub, previous, first);
    }

    private int AddVert(VertexHelper vh, Vector2 p, float v, Color vertexColor)
    {
        vh.AddVert(new Vector3(p.x, p.y, 0f), vertexColor * color, new Vector2(0.5f, v));
        return vh.currentVertCount - 1;
    }

    /// <summary>The line's color a given distance along it, easing out of the parent node's color
    /// into the child's over <see cref="ManorBranchLayout.ColorBlendLength"/>.</summary>
    private static Color BlendAt(float distance, Color from, Color to)
    {
        float length = ManorBranchLayout.ColorBlendLength;
        if (length <= Epsilon) return to;
        return Color.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / length)));
    }

    private static void AppendArc(List<(Vector2 P, float V)> into, Vector2 centre, float radius,
                                  Vector2 fromPoint, Vector2 toPoint)
    {
        float a0 = Mathf.Atan2(fromPoint.y - centre.y, fromPoint.x - centre.x);
        float a1 = Mathf.Atan2(toPoint.y - centre.y, toPoint.x - centre.x);
        // Corners are right angles, so the short way round is always the arc we want.
        float sweep = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;

        for (int t = 0; t <= ArcSteps; t++)
        {
            float angle = a0 + sweep * t / ArcSteps;
            into.Add((centre + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 0f));
        }
    }

    private static void Dedupe(List<(Vector2 P, float V)> points)
    {
        for (int i = points.Count - 1; i > 0; i--)
            if ((points[i].P - points[i - 1].P).sqrMagnitude < Epsilon * Epsilon) points.RemoveAt(i);
    }

    // ── Antialiasing ramp ────────────────────────────────────────────────────

    /// <summary>Coverage lookup: UV.y is <c>distance-to-outline / thickness</c>, so the sprite is a
    /// one-dimensional ramp from transparent at the outline to solid
    /// <see cref="ManorBranchLayout.EdgeFeather"/> units inside it.</summary>
    private static Texture Ramp
    {
        get
        {
            if (s_ramp == null) s_ramp = BuildRamp();
            return s_ramp;
        }
    }

    private static Texture2D BuildRamp()
    {
        const int Height = 128;

        var texture = new Texture2D(1, Height, TextureFormat.RGBA32, mipChain: false)
        {
            name = "AP_ManorBranchRamp",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            // Survives scene loads and is never written to disk, matching the overlay's lifetime.
            hideFlags = HideFlags.HideAndDontSave,
        };

        float feather = Mathf.Max(0.01f, ManorBranchLayout.EdgeFeather);
        for (int i = 0; i < Height; i++)
        {
            float distance = (i + 0.5f) / Height * ManorBranchLayout.Thickness;
            texture.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.Clamp01(distance / feather)));
        }

        texture.Apply();
        return texture;
    }
}
