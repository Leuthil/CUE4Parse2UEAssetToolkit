﻿using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.UObject;
using UEATSerializer.UEAT;

namespace CUE4Parse2UEAT.Factory
{
    public static class PackageObjectUtils
    {
        public static PackageObject? CreatePackageObject(FPackageObjectIndex? packageObjectIndex, IoPackage package)
        {
            if (packageObjectIndex == null || packageObjectIndex.Value.IsNull)
            {
                return null;
            }

            var resolvedObject = package.ResolveObjectIndex(packageObjectIndex.Value);

            return CreatePackageObject(resolvedObject, package);
        }

        public static PackageObject? CreatePackageObject(ResolvedObject? resolvedObject, IoPackage package)
        {
            if (resolvedObject == null)
            {
                return null;
            }

            var objectName = resolvedObject.Name.Text;
            var objectPackage = GetPackage(resolvedObject);
            var packageName = objectPackage?.Name.Text;

            // import
            if (!package.Name.Equals(packageName))
            {
                return CreateImportPackageObject(packageName, objectName, resolvedObject, package);
            }

            return CreateExportPackageObject(packageName, objectName, resolvedObject.Class, resolvedObject.Outer, (int)resolvedObject.Object.Value.Flags, package);
        }

        private static PackageObject CreateImportPackageObject(string? packageName, string objectName, ResolvedObject? resolvedObject, IoPackage package)
        {
            var classPackage = resolvedObject.Class?.Outer?.Name.Text;
            var className = resolvedObject.Class?.Name.Text;

            // fixups

            if (resolvedObject.Class?.Outer == null)
            {
                classPackage = resolvedObject.Outer?.Name.Text;
            }

            // CDOs
            if ("Class".Equals(className) && objectName.StartsWith("Default__"))
            {
                className = objectName.Substring("Default__".Length);
            }

            // class (should this be combined with the above fixup for CDOs?)
            if ("Class".Equals(className))
            {
                classPackage = "/Script/CoreUObject";

                // workaround because CUE4Parse cannot differentiate between UScriptStruct and UClass, so everything is classified as "Class"
                // see code comment within CUE4Parse.UE4.Assets.ResolvedScriptObject
                if (package?.Provider?.MappingsForGame != null
                    && package.Provider.MappingsForGame.Types.TryGetValue(objectName, out var type))
                {
                    var superType = type;

                    while (superType?.Super.Value != null)
                    {
                        superType = superType.Super.Value;
                    }

                    // no idea if this is a legit check, but it seems to work so far
                    bool isStruct = !("Object".Equals(superType.Name));
                    if (isStruct)
                    {
                        className = "ScriptStruct";
                    }
                }

            }

            // packages
            // if Outer is null then this is a package
            if (resolvedObject.Outer == null)
            {
                classPackage = "/Script/CoreUObject";
                className = "Package";
            }

            var import = new ImportPackageObject();
            import.PackageName = packageName;
            import.ObjectName = objectName;
            import.ClassPackage = classPackage;
            import.ClassName = className;
            import.Outer = CreatePackageObject(resolvedObject?.Outer, package);

            return import;
        }

        public static PackageObject? CreatePackageObject(FExportMapEntry exportMapEntry, IoPackage package)
        {
            var classObject = package.ResolveObjectIndex(exportMapEntry.ClassIndex);
            var outerObject = package.ResolveObjectIndex(exportMapEntry.OuterIndex);
            var objectName = CreateFNameFromMappedName(exportMapEntry.ObjectName, package.GlobalData.GlobalNameMap, package.NameMap).Text;
            var packageName = package.Name;

            return CreateExportPackageObject(packageName, objectName, classObject, outerObject, (int)exportMapEntry.ObjectFlags, package);
        }

        public static ExportPackageObject CreateExportPackageObject(string packageName, string objectName, ResolvedObject? objectClass, ResolvedObject? outer, int objectFlags, IoPackage package)
        {
            var export = new ExportPackageObject();
            export.PackageName = packageName;
            export.ObjectName = objectName;
            export.ObjectClass = CreatePackageObject(objectClass, package);
            export.ObjectFlags = objectFlags;
            export.Outer = CreatePackageObject(outer, package);
            return export;
        }

        public static ResolvedObject? GetPackage(ResolvedObject? resolvedObject)
        {
            var package = resolvedObject;

            while (package.Outer != null)
            {
                package = package.Outer;
            }

            return package;
        }

        public static FName CreateFNameFromMappedName(FMappedName mappedName, FNameEntrySerialized[] globalNameMap, FNameEntrySerialized[] nameMap) =>
            new(mappedName, mappedName.IsGlobal ? globalNameMap : nameMap);
    }
}
