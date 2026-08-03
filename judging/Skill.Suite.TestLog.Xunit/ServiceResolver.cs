using System.Reflection;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// Reflection-based locator that resolves the single implementation of each
/// contract interface and wires constructor dependencies recursively.
/// Per the test-project rule: when multiple implementations satisfy an
/// interface, the alphabetically first concrete type wins.
/// </summary>
public static class ServiceResolver
{
    private static readonly Dictionary<Type, object> Cache = new();
    private static readonly object Sync = new();
    private static bool _assembliesLoaded;

    /// <summary>Resolve the implementation of <typeparamref name="T"/>.</summary>
    public static T Resolve<T>() where T : class => (T)Resolve(typeof(T));

    /// <summary>Resolve the implementation of <paramref name="contractType"/>.</summary>
    public static object Resolve(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (!contractType.IsInterface)
        {
            throw new ArgumentException(
                $"{contractType.FullName} is not an interface.", nameof(contractType));
        }

        lock (Sync)
        {
            return ResolveLocked(contractType, new HashSet<Type>());
        }
    }

    /// <summary>Drop all cached instances. Useful for test isolation.</summary>
    public static void Reset()
    {
        lock (Sync) Cache.Clear();
    }

    /// <summary>
    /// Constructs a fresh instance of the implementation of <typeparamref name="T"/>
    /// using the supplied dependencies for constructor parameters. Does not consult
    /// or update the singleton cache — safe to use alongside parallel test classes
    /// that rely on <see cref="Resolve{T}"/>.
    /// </summary>
    public static T BuildWith<T>(params object[] dependencies) where T : class =>
        (T)BuildWith(typeof(T), dependencies);

    /// <summary>Non-generic overload of <see cref="BuildWith{T}"/>.</summary>
    public static object BuildWith(Type contractType, params object[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (!contractType.IsInterface)
        {
            throw new ArgumentException(
                $"{contractType.FullName} is not an interface.", nameof(contractType));
        }

        lock (Sync)
        {
            EnsureAssembliesLoaded();
            var implType = FindImplementation(contractType)
                ?? throw new InvalidOperationException(
                    $"No concrete implementation of {contractType.FullName} was found.");

            var ctors = implType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToList();
            if (ctors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{implType.FullName} has no public constructor.");
            }

            Exception? lastFailure = null;
            foreach (var ctor in ctors)
            {
                var parameters = ctor.GetParameters();
                var args = new object?[parameters.Length];
                var matched = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var pt = parameters[i].ParameterType;
                    object? candidate = null;
                    foreach (var dep in dependencies)
                    {
                        if (dep is not null && pt.IsInstanceOfType(dep))
                        {
                            candidate = dep;
                            break;
                        }
                    }
                    if (candidate is null)
                    {
                        matched = false;
                        lastFailure = new InvalidOperationException(
                            $"No supplied dependency satisfies parameter '{parameters[i].Name}' " +
                            $"of type {pt.FullName} on {implType.FullName}.");
                        break;
                    }
                    args[i] = candidate;
                }
                if (matched) return ctor.Invoke(args);
            }

            throw new InvalidOperationException(
                $"No constructor on {implType.FullName} could be satisfied with the supplied dependencies.",
                lastFailure);
        }
    }

    private static object ResolveLocked(Type contractType, HashSet<Type> inProgress)
    {
        if (Cache.TryGetValue(contractType, out var cached)) return cached;

        EnsureAssembliesLoaded();

        if (!inProgress.Add(contractType))
        {
            throw new InvalidOperationException(
                $"Cyclic dependency detected while resolving {contractType.FullName}.");
        }

        try
        {
            var implType = FindImplementation(contractType)
                ?? throw new InvalidOperationException(
                    $"No concrete implementation of {contractType.FullName} was found in loaded assemblies.");

            var instance = Construct(implType, inProgress);
            Cache[contractType] = instance;
            return instance;
        }
        finally
        {
            inProgress.Remove(contractType);
        }
    }

    private static object Construct(Type implType, HashSet<Type> inProgress)
    {
        // Try every public constructor, widest first, so ones with the most
        // dependencies win when both could be satisfied.
        var ctors = implType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        if (ctors.Count == 0)
        {
            throw new InvalidOperationException(
                $"{implType.FullName} has no public constructor.");
        }

        Exception? lastFailure = null;
        foreach (var ctor in ctors)
        {
            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            var satisfied = true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var pt = parameters[i].ParameterType;
                if (!pt.IsInterface)
                {
                    satisfied = false;
                    lastFailure = new InvalidOperationException(
                        $"Constructor parameter '{parameters[i].Name}' of {implType.FullName} " +
                        $"is not a contract interface ({pt.FullName}).");
                    break;
                }

                try
                {
                    args[i] = ResolveLocked(pt, inProgress);
                }
                catch (Exception ex)
                {
                    satisfied = false;
                    lastFailure = ex;
                    break;
                }
            }

            if (satisfied) return ctor.Invoke(args);
        }

        throw new InvalidOperationException(
            $"No public constructor on {implType.FullName} could be satisfied from contract interfaces.",
            lastFailure);
    }

    private static Type? FindImplementation(Type contractType)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            // Skip dynamically-emitted assemblies — these contain runtime proxies
            // from mock frameworks (NSubstitute / Castle DynamicProxy) that
            // would otherwise compete with real implementations.
            .Where(a => !a.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsInterface: false, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(contractType.IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static void EnsureAssembliesLoaded()
    {
        if (_assembliesLoaded) return;
        _assembliesLoaded = true;

        // Walk the reference graph so referenced-but-unloaded assemblies enter the AppDomain.
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? string.Empty),
            StringComparer.Ordinal);

        var queue = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        while (queue.Count > 0)
        {
            var asm = queue.Dequeue();
            foreach (var refName in asm.GetReferencedAssemblies())
            {
                if (refName.Name is null || !loaded.Add(refName.Name)) continue;
                try { queue.Enqueue(Assembly.Load(refName)); }
                catch { /* ignore unresolvable refs */ }
            }
        }

        // Fallback: pick up implementation assemblies sitting next to the test host
        // that aren't reached through references.
        try
        {
            foreach (var dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (loaded.Contains(name)) continue;
                try
                {
                    var asm = Assembly.LoadFrom(dll);
                    loaded.Add(asm.GetName().Name ?? name);
                }
                catch { /* ignore unloadable dlls */ }
            }
        }
        catch { /* ignore directory access errors */ }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
        catch { return Array.Empty<Type>(); }
    }
}
