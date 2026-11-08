namespace Orbitera.Domain;

public sealed class DevelopmentInsightsRules
{
    public DevelopmentInsightsRules(
        IEnumerable<ProjectIndicatorRule>? projectIndicators = null,
        IEnumerable<DevelopmentInsightRule>? artifactRules = null)
    {
        ProjectIndicators = (projectIndicators ?? CreateDefaultProjectIndicators()).ToArray();
        ArtifactRules = (artifactRules ?? CreateDefaultArtifactRules()).ToArray();
    }

    public IReadOnlyList<ProjectIndicatorRule> ProjectIndicators { get; }

    public IReadOnlyList<DevelopmentInsightRule> ArtifactRules { get; }

    private static IEnumerable<ProjectIndicatorRule> CreateDefaultProjectIndicators()
    {
        yield return new("*.csproj", DevelopmentEcosystem.DotNet);
        yield return new("*.fsproj", DevelopmentEcosystem.DotNet);
        yield return new("*.vbproj", DevelopmentEcosystem.DotNet);
        yield return new("*.sln", DevelopmentEcosystem.DotNet);
        yield return new("*.slnx", DevelopmentEcosystem.DotNet);
        yield return new("package.json", DevelopmentEcosystem.NodeJs);
        yield return new("pyproject.toml", DevelopmentEcosystem.Python);
        yield return new("requirements.txt", DevelopmentEcosystem.Python);
        yield return new("Pipfile", DevelopmentEcosystem.Python);
        yield return new("setup.py", DevelopmentEcosystem.Python);
        yield return new("setup.cfg", DevelopmentEcosystem.Python);
        yield return new(".git", DevelopmentEcosystem.Git, IsDirectory: true);
    }

    private static IEnumerable<DevelopmentInsightRule> CreateDefaultArtifactRules()
    {
        yield return new(
            "bin", DevelopmentEcosystem.DotNet, DevelopmentInsightCategory.BuildArtifact,
            "Contiene ensamblados y otros resultados compilados de .NET.",
            "Puede regenerarse compilando el proyecto; se perderán cambios locales presentes solo en esos resultados.",
            DevelopmentRiskLevel.Low, true);
        yield return new(
            "obj", DevelopmentEcosystem.DotNet, DevelopmentInsightCategory.BuildArtifact,
            "Contiene archivos intermedios generados durante compilaciones de .NET.",
            "La siguiente compilación puede tardar más porque MSBuild regenerará los archivos intermedios.",
            DevelopmentRiskLevel.Low, true);
        yield return new(
            ".vs", DevelopmentEcosystem.DotNet, DevelopmentInsightCategory.Cache,
            "Guarda estado de workspace, cachés e información local de Visual Studio.",
            "Se pueden restablecer preferencias o estado local de Visual Studio; el IDE lo vuelve a crear.",
            DevelopmentRiskLevel.Moderate, true);
        yield return new(
            "node_modules", DevelopmentEcosystem.NodeJs, DevelopmentInsightCategory.Dependency,
            "Contiene dependencias instaladas para un proyecto Node.js.",
            "La aplicación puede dejar de compilar o ejecutarse hasta reinstalar dependencias con el gestor del proyecto.",
            DevelopmentRiskLevel.Moderate, true);
        yield return new(
            "dist", DevelopmentEcosystem.NodeJs, DevelopmentInsightCategory.BuildArtifact,
            "Suele contener paquetes de distribución o resultados de compilación de proyectos Node.js.",
            "La carpeta puede contener entregables publicados; confirma que se puedan regenerar antes de considerar limpieza.",
            DevelopmentRiskLevel.High, true);
        yield return new(
            "build", DevelopmentEcosystem.NodeJs, DevelopmentInsightCategory.BuildArtifact,
            "Puede contener resultados de compilación de un proyecto Node.js.",
            "El nombre no garantiza que el contenido sea generado; podría incluir archivos fuente o entregables únicos.",
            DevelopmentRiskLevel.High, true);
        yield return new(
            ".next", DevelopmentEcosystem.NodeJs, DevelopmentInsightCategory.BuildArtifact,
            "Contiene resultados de compilación y caché del framework Next.js.",
            "Next.js puede regenerar los resultados, pero una compilación posterior puede tardar más.",
            DevelopmentRiskLevel.Moderate, true,
            ["next.config.js", "next.config.mjs", "next.config.ts", "next.config.cjs"]);
        yield return new(
            ".venv", DevelopmentEcosystem.Python, DevelopmentInsightCategory.Dependency,
            "Contiene un entorno virtual local de Python con intérprete y dependencias instaladas.",
            "El proyecto puede perder sus dependencias locales hasta recrear el entorno virtual.",
            DevelopmentRiskLevel.Moderate, true);
        yield return new(
            "venv", DevelopmentEcosystem.Python, DevelopmentInsightCategory.Dependency,
            "Contiene un posible entorno virtual local de Python con dependencias instaladas.",
            "Confirma que sea un entorno virtual del proyecto y no datos del usuario; puede requerir reinstalar dependencias.",
            DevelopmentRiskLevel.High, true);
        yield return new(
            "__pycache__", DevelopmentEcosystem.Python, DevelopmentInsightCategory.Cache,
            "Contiene bytecode Python generado para acelerar importaciones.",
            "Python regenerará el bytecode; la primera importación posterior puede ser más lenta.",
            DevelopmentRiskLevel.Low, true);
        yield return new(
            ".pytest_cache", DevelopmentEcosystem.Python, DevelopmentInsightCategory.Cache,
            "Contiene caché y metadatos de ejecuciones de pytest.",
            "Se perderán datos locales de caché de pytest, que se regeneran al volver a ejecutar pruebas.",
            DevelopmentRiskLevel.Low, true,
            ["pytest.ini", "pyproject.toml", "setup.cfg", "tox.ini"]);
        yield return new(
            ".git", DevelopmentEcosystem.Git, DevelopmentInsightCategory.VersionControl,
            "Contiene el historial, referencias, configuración y metadatos del repositorio Git.",
            "Eliminarlo puede destruir el historial local, ramas no publicadas y trabajo que no exista en otro lugar.",
            DevelopmentRiskLevel.High, false);
    }
}
