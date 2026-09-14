using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace Dungeon;

internal sealed class DungeonGame : GameWindow
{
    private const int MapWidth = 25;
    private const int MapHeight = 25;
    private const float Tile = 2.4f;
    private const float WallHeight = 2.9f;
    private const float EyeHeight = 1.66f;
    private const float PlayerRadius = 0.28f;
    private const int MaxLights = 8;

    private readonly bool[,] walkable = new bool[MapWidth, MapHeight];
    private readonly List<Light> lights = [];
    private Mesh? dungeonMesh;
    private Mesh? torchMesh;
    private Mesh? crystalMesh;
    private int shader;
    private int albedoTexture;
    private int normalTexture;
    private Vector3 position = new(1.5f * Tile, EyeHeight, 1.5f * Tile);
    private Vector3 velocity;
    private float yaw = MathHelper.PiOver2;
    private float pitch;
    private float travelled;
    private float headBob;
    private bool mouseCaptured = true;

    public DungeonGame(GameWindowSettings gameSettings, NativeWindowSettings windowSettings)
        : base(gameSettings, windowSettings)
    {
        GenerateDungeon();
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;
        CursorState = CursorState.Grabbed;
        GL.ClearColor(0.0015f, 0.0025f, 0.006f, 1f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.Multisample);
        GL.CullFace(TriangleFace.Back);
        shader = CreateShader(VertexShader, FragmentShader);
        string texturePath = Path.Combine(AppContext.BaseDirectory, "Assets", "dark_basalt.png");
        (albedoTexture, normalTexture) = LoadMaterial(texturePath);
        BuildMeshes();
    }

    protected override void OnUnload()
    {
        dungeonMesh?.Dispose();
        torchMesh?.Dispose();
        crystalMesh?.Dispose();
        GL.DeleteTexture(albedoTexture);
        GL.DeleteTexture(normalTexture);
        GL.DeleteProgram(shader);
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        if (!IsFocused) return;

        if (KeyboardState.IsKeyPressed(Keys.Escape))
        {
            if (mouseCaptured)
            {
                mouseCaptured = false;
                CursorState = CursorState.Normal;
            }
            else Close();
        }
        if (KeyboardState.IsKeyPressed(Keys.F11))
            WindowState = WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen;
        if (!mouseCaptured && MouseState.IsButtonPressed(MouseButton.Left))
        {
            mouseCaptured = true;
            CursorState = CursorState.Grabbed;
        }

        float dt = Math.Min((float)e.Time, 0.05f);
        if (mouseCaptured)
        {
            yaw += MouseState.Delta.X * 0.0022f;
            pitch -= MouseState.Delta.Y * 0.0020f;
            pitch = Math.Clamp(pitch, -1.42f, 1.42f);
        }

        Vector3 forward = new(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 desired = Vector3.Zero;
        if (KeyboardState.IsKeyDown(Keys.W)) desired += forward;
        if (KeyboardState.IsKeyDown(Keys.S)) desired -= forward;
        if (KeyboardState.IsKeyDown(Keys.D)) desired += right;
        if (KeyboardState.IsKeyDown(Keys.A)) desired -= right;
        if (desired.LengthSquared > 1f) desired = Vector3.Normalize(desired);
        float speed = KeyboardState.IsKeyDown(Keys.LeftShift) ? 5.5f : 3.35f;
        desired *= speed;
        float response = 1f - MathF.Exp(-12f * dt);
        velocity.X += (desired.X - velocity.X) * response;
        velocity.Z += (desired.Z - velocity.Z) * response;

        float nextX = position.X + velocity.X * dt;
        float nextZ = position.Z + velocity.Z * dt;
        if (CanStand(nextX, position.Z)) position.X = nextX; else velocity.X = 0;
        if (CanStand(position.X, nextZ)) position.Z = nextZ; else velocity.Z = 0;
        float planarSpeed = new Vector2(velocity.X, velocity.Z).Length;
        travelled += planarSpeed * dt;
        float bobTarget = planarSpeed > 0.2f ? MathF.Sin(travelled * 7.5f) * 0.035f : 0f;
        headBob += (bobTarget - headBob) * Math.Min(1f, 10f * dt);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.UseProgram(shader);

        Vector3 eye = position + Vector3.UnitY * headBob;
        Vector3 front = new(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch));
        Matrix4 view = Matrix4.LookAt(eye, eye + front, Vector3.UnitY);
        float aspect = ClientSize.Y == 0 ? 1f : ClientSize.X / (float)ClientSize.Y;
        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(70f), aspect, 0.07f, 72f);
        SetMatrix("view", view);
        SetMatrix("projection", projection);
        GL.Uniform3(GL.GetUniformLocation(shader, "eyePosition"), eye);
        GL.Uniform1(GL.GetUniformLocation(shader, "time"), (float)GLFW.GetTime());

        BindMaterial();
        UploadLights(eye);
        SetMaterial(false, Vector3.One);
        dungeonMesh?.Draw();

        GL.Disable(EnableCap.CullFace);
        SetMaterial(true, new Vector3(1f, 0.09f, 0.012f));
        torchMesh?.Draw();
        SetMaterial(true, new Vector3(0.012f, 0.16f, 0.72f));
        crystalMesh?.Draw();
        GL.Enable(EnableCap.CullFace);

        SwapBuffers();
    }

    private void BindMaterial()
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, albedoTexture);
        GL.Uniform1(GL.GetUniformLocation(shader, "albedoMap"), 0);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, normalTexture);
        GL.Uniform1(GL.GetUniformLocation(shader, "normalMap"), 1);
    }

    private void SetMaterial(bool emissive, Vector3 tint)
    {
        GL.Uniform1(GL.GetUniformLocation(shader, "emissive"), emissive ? 1f : 0f);
        GL.Uniform3(GL.GetUniformLocation(shader, "tint"), tint);
    }

    private void UploadLights(Vector3 eye)
    {
        List<Light> nearest = lights.OrderBy(l => Vector3.DistanceSquared(l.Position, eye)).Take(MaxLights - 1).ToList();
        GL.Uniform1(GL.GetUniformLocation(shader, "lightCount"), nearest.Count + 1);
        SetLight(0, eye + new Vector3(0, -0.18f, 0), new Vector3(0.22f, 0.105f, 0.038f));
        for (int i = 0; i < nearest.Count; i++) SetLight(i + 1, nearest[i].Position, nearest[i].Color);
    }

    private void SetLight(int index, Vector3 pos, Vector3 color)
    {
        GL.Uniform3(GL.GetUniformLocation(shader, $"lightPositions[{index}]"), pos);
        GL.Uniform3(GL.GetUniformLocation(shader, $"lightColors[{index}]"), color);
    }

    private void SetMatrix(string name, Matrix4 matrix)
        => GL.UniformMatrix4(GL.GetUniformLocation(shader, name), false, ref matrix);

    private void GenerateDungeon()
    {
        Random random = new(1337);
        Stack<Vector2i> stack = new();
        int[] dx = [0, 2, 0, -2];
        int[] dz = [-2, 0, 2, 0];
        walkable[1, 1] = true;
        stack.Push(new Vector2i(1, 1));
        while (stack.Count > 0)
        {
            Vector2i p = stack.Peek();
            List<int> choices = [];
            for (int d = 0; d < 4; d++)
            {
                int nx = p.X + dx[d], nz = p.Y + dz[d];
                if (nx > 0 && nz > 0 && nx < MapWidth - 1 && nz < MapHeight - 1 && !walkable[nx, nz]) choices.Add(d);
            }
            if (choices.Count == 0) { stack.Pop(); continue; }
            int selected = choices[random.Next(choices.Count)];
            int tx = p.X + dx[selected], tz = p.Y + dz[selected];
            walkable[p.X + dx[selected] / 2, p.Y + dz[selected] / 2] = true;
            walkable[tx, tz] = true;
            stack.Push(new Vector2i(tx, tz));
        }
        CarveRoom(8, 3, 4, 4); CarveRoom(15, 10, 5, 4); CarveRoom(3, 17, 5, 4); CarveRoom(17, 19, 4, 3);

        lights.Add(new Light(new Vector3(1.5f * Tile, 1.72f, 1.5f * Tile + 0.65f), new Vector3(5.8f, 1.35f, 0.16f), false));
        int n = 0;
        for (int z = 1; z < MapHeight - 1; z++)
        for (int x = 1; x < MapWidth - 1; x++)
        {
            if (!walkable[x, z]) continue;
            n++;
            Vector3 p = new((x + 0.5f) * Tile, 1.7f, (z + 0.5f) * Tile);
            if (n % 11 == 0) lights.Add(new Light(p, new Vector3(5.5f, 1.2f, 0.13f), false));
            else if (n % 31 == 0) lights.Add(new Light(p with { Y = 0.7f }, new Vector3(0.08f, 1.1f, 4.2f), true));
        }
    }

    private void CarveRoom(int left, int top, int width, int height)
    {
        for (int z = top; z < top + height && z < MapHeight - 1; z++)
        for (int x = left; x < left + width && x < MapWidth - 1; x++) walkable[x, z] = true;
    }

    private bool CanStand(float x, float z)
        => IsOpen(x - PlayerRadius, z - PlayerRadius) && IsOpen(x + PlayerRadius, z - PlayerRadius)
        && IsOpen(x - PlayerRadius, z + PlayerRadius) && IsOpen(x + PlayerRadius, z + PlayerRadius);

    private bool IsOpen(float x, float z)
    {
        int mx = (int)MathF.Floor(x / Tile), mz = (int)MathF.Floor(z / Tile);
        return mx >= 0 && mz >= 0 && mx < MapWidth && mz < MapHeight && walkable[mx, mz];
    }

    private bool OpenCell(int x, int z)
        => x >= 0 && z >= 0 && x < MapWidth && z < MapHeight && walkable[x, z];

    private void BuildMeshes()
    {
        MeshBuilder dungeon = new();
        for (int z = 0; z < MapHeight; z++)
        for (int x = 0; x < MapWidth; x++)
        {
            if (!walkable[x, z]) continue;
            float x0 = x * Tile, x1 = x0 + Tile, z0 = z * Tile, z1 = z0 + Tile;
            dungeon.Quad(new(x0, 0, z0), new(x0, 0, z1), new(x1, 0, z1), new(x1, 0, z0), Vector3.UnitY, 1.6f, 1.6f);
            dungeon.Quad(new(x1, WallHeight, z0), new(x1, WallHeight, z1), new(x0, WallHeight, z1), new(x0, WallHeight, z0), -Vector3.UnitY, 1.6f, 1.6f);
            if (!OpenCell(x, z - 1)) dungeon.Quad(new(x0, 0, z0), new(x1, 0, z0), new(x1, WallHeight, z0), new(x0, WallHeight, z0), Vector3.UnitZ, 1.5f, 1.8f);
            if (!OpenCell(x, z + 1)) dungeon.Quad(new(x1, 0, z1), new(x0, 0, z1), new(x0, WallHeight, z1), new(x1, WallHeight, z1), -Vector3.UnitZ, 1.5f, 1.8f);
            if (!OpenCell(x - 1, z)) dungeon.Quad(new(x0, 0, z1), new(x0, 0, z0), new(x0, WallHeight, z0), new(x0, WallHeight, z1), Vector3.UnitX, 1.5f, 1.8f);
            if (!OpenCell(x + 1, z)) dungeon.Quad(new(x1, 0, z0), new(x1, 0, z1), new(x1, WallHeight, z1), new(x1, WallHeight, z0), -Vector3.UnitX, 1.5f, 1.8f);
        }
        dungeonMesh = dungeon.Build();

        MeshBuilder torches = new(), crystals = new();
        foreach (Light light in lights)
        {
            if (light.IsCrystal) crystals.Octahedron(light.Position, 0.22f, 0.52f);
            else torches.Octahedron(light.Position, 0.12f, 0.27f);
        }
        torchMesh = torches.Build();
        crystalMesh = crystals.Build();
    }

    private static (int Albedo, int Normal) LoadMaterial(string path)
    {
        using Bitmap source = new(path);
        using Bitmap image = source.Clone(new Rectangle(0, 0, source.Width, source.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Rectangle bounds = new(0, 0, image.Width, image.Height);
        BitmapData locked = image.LockBits(bounds, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int stride = Math.Abs(locked.Stride);
        byte[] pixels = new byte[stride * image.Height];
        Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);
        image.UnlockBits(locked);

        byte[] normals = new byte[pixels.Length];
        float Height(int x, int y)
        {
            x = (x + image.Width) % image.Width;
            y = (y + image.Height) % image.Height;
            int i = y * stride + x * 4;
            return (pixels[i] * 0.0722f + pixels[i + 1] * 0.7152f + pixels[i + 2] * 0.2126f) / 255f;
        }
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            Vector3 n = Vector3.Normalize(new Vector3((Height(x - 1, y) - Height(x + 1, y)) * 4.8f, (Height(x, y - 1) - Height(x, y + 1)) * 4.8f, 1f));
            int i = y * stride + x * 4;
            normals[i] = (byte)((n.Z * 0.5f + 0.5f) * 255); // B
            normals[i + 1] = (byte)((n.Y * 0.5f + 0.5f) * 255); // G
            normals[i + 2] = (byte)((n.X * 0.5f + 0.5f) * 255); // R
            normals[i + 3] = 255;
        }
        return (UploadTexture(image.Width, image.Height, pixels), UploadTexture(image.Width, image.Height, normals));
    }

    private static int UploadTexture(int width, int height, byte[] pixels)
    {
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Srgb8Alpha8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxAnisotropy, 8f);
        return id;
    }

    private static int CreateShader(string vertexSource, string fragmentSource)
    {
        int vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        int fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);
        int result = GL.CreateProgram();
        GL.AttachShader(result, vertex); GL.AttachShader(result, fragment); GL.LinkProgram(result);
        GL.GetProgram(result, GetProgramParameterName.LinkStatus, out int linked);
        GL.DeleteShader(vertex); GL.DeleteShader(fragment);
        if (linked == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(result));
        return result;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int result = GL.CreateShader(type);
        GL.ShaderSource(result, source); GL.CompileShader(result);
        GL.GetShader(result, ShaderParameter.CompileStatus, out int compiled);
        if (compiled == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(result));
        return result;
    }

    private readonly record struct Light(Vector3 Position, Vector3 Color, bool IsCrystal);

    private sealed class MeshBuilder
    {
        private readonly List<float> vertices = [];
        private readonly List<uint> indices = [];

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float u, float v)
        {
            uint start = (uint)(vertices.Count / 8);
            Vertex(a, normal, new(0, 0)); Vertex(b, normal, new(u, 0)); Vertex(c, normal, new(u, v)); Vertex(d, normal, new(0, v));
            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }

        public void Octahedron(Vector3 p, float radius, float height)
        {
            Vector3 top = p + Vector3.UnitY * height, bottom = p - Vector3.UnitY * height * 0.42f;
            Vector3 a = p + new Vector3(-radius, 0, -radius), b = p + new Vector3(radius, 0, -radius);
            Vector3 c = p + new Vector3(radius, 0, radius), d = p + new Vector3(-radius, 0, radius);
            Triangle(top, a, b); Triangle(top, b, c); Triangle(top, c, d); Triangle(top, d, a);
            Triangle(bottom, b, a); Triangle(bottom, c, b); Triangle(bottom, d, c); Triangle(bottom, a, d);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            uint start = (uint)(vertices.Count / 8);
            Vertex(a, n, new(0.5f, 1)); Vertex(b, n, new(0, 0)); Vertex(c, n, new(1, 0));
            indices.AddRange([start, start + 1, start + 2]);
        }

        private void Vertex(Vector3 p, Vector3 n, Vector2 uv)
            => vertices.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, uv.X, uv.Y]);

        public Mesh Build() => new([.. vertices], [.. indices]);
    }

    private sealed class Mesh : IDisposable
    {
        private readonly int vao;
        private readonly int vbo;
        private readonly int ebo;
        private readonly int indexCount;

        public Mesh(float[] vertices, uint[] indices)
        {
            indexCount = indices.Length;
            vao = GL.GenVertexArray(); vbo = GL.GenBuffer(); ebo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0); GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float)); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float)); GL.EnableVertexAttribArray(2);
            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            if (indexCount == 0) return;
            GL.BindVertexArray(vao);
            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao); GL.DeleteBuffer(vbo); GL.DeleteBuffer(ebo);
        }
    }

    private const string VertexShader = """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in vec2 aUv;
        uniform mat4 view;
        uniform mat4 projection;
        out vec3 worldPosition;
        out vec3 geometricNormal;
        out vec2 uv;
        void main() {
            worldPosition = aPosition;
            geometricNormal = aNormal;
            uv = aUv;
            gl_Position = projection * view * vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        const int MAX_LIGHTS = 8;
        uniform sampler2D albedoMap;
        uniform sampler2D normalMap;
        uniform vec3 eyePosition;
        uniform vec3 lightPositions[MAX_LIGHTS];
        uniform vec3 lightColors[MAX_LIGHTS];
        uniform int lightCount;
        uniform float time;
        uniform float emissive;
        uniform vec3 tint;
        in vec3 worldPosition;
        in vec3 geometricNormal;
        in vec2 uv;
        out vec4 outputColor;

        void main() {
            if (emissive > 0.5) {
                float pulse = 0.82 + sin(time * 8.0 + worldPosition.x * 1.7 + worldPosition.z) * 0.18;
                outputColor = vec4(tint * (3.5 * pulse), 1.0);
                return;
            }

            vec3 G = normalize(geometricNormal);
            vec3 dpdx = dFdx(worldPosition), dpdy = dFdy(worldPosition);
            vec2 duvdx = dFdx(uv), duvdy = dFdy(uv);
            vec3 T = normalize(dpdx * duvdy.y - dpdy * duvdx.y);
            T = normalize(T - G * dot(G, T));
            vec3 B = normalize(cross(G, T));
            vec3 mapNormal = texture(normalMap, uv).rgb * 2.0 - 1.0;
            vec3 N = normalize(T * mapNormal.x + B * mapNormal.y + G * mapNormal.z);
            vec3 V = normalize(eyePosition - worldPosition);
            vec3 albedo = texture(albedoMap, uv).rgb * tint;
            vec3 lighting = vec3(0.006, 0.008, 0.014);

            for (int i = 0; i < lightCount; i++) {
                vec3 delta = lightPositions[i] - worldPosition;
                float distanceToLight = length(delta);
                vec3 L = delta / max(distanceToLight, 0.001);
                float radiusFalloff = pow(clamp(1.0 - distanceToLight / 10.5, 0.0, 1.0), 2.0);
                float attenuation = radiusFalloff / (1.0 + 0.07 * distanceToLight * distanceToLight);
                float diffuse = max(dot(N, L), 0.0);
                float specular = pow(max(dot(reflect(-L, N), V), 0.0), 34.0) * 0.22;
                float flicker = i == 0 ? 1.0 : 0.90 + sin(time * 8.7 + float(i) * 2.31) * 0.10;
                lighting += lightColors[i] * (diffuse + specular) * attenuation * flicker;
            }

            vec3 hdr = albedo * lighting;
            float distanceFromEye = length(eyePosition - worldPosition);
            float fogNoise = sin(worldPosition.x * 0.31 + worldPosition.z * 0.19) * 0.04;
            float fog = clamp(1.0 - exp(-distanceFromEye * 0.075) + fogNoise, 0.0, 0.94);
            hdr = mix(hdr, vec3(0.003, 0.006, 0.013), fog);
            vec3 mapped = hdr / (hdr + vec3(1.0));
            outputColor = vec4(pow(mapped, vec3(1.0 / 2.2)), 1.0);
        }
        """;
}
