// Generates Unity .meta files (stable GUIDs derived from each path) and the UdonSharp
// assets that Unity packages must ship: one UdonSharpProgramAsset per concrete U# behaviour
// and one UdonSharpAssemblyDefinition per runtime asmdef.
//
//   node tools/generate-meta.mjs          # create anything missing (never changes existing GUIDs)
//
// Formats match what shipping VRChat packages (e.g. ProTV 3) contain.

import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const PACKAGES = join(ROOT, "Packages");

// UdonSharp's own script GUIDs (identical in every VRChat SDK install).
const USHARP_PROGRAM_ASSET_SCRIPT = "c333ccfdd0cbdbc4ca30cef2dd6e6b9b";
const USHARP_ASMDEF_ASSET_SCRIPT = "5136146375e9a0a498a72a0091b40cc1";
const ASMDEF_FILE_ID = "5897886265953266890";

const guidFor = (path) => createHash("md5").update(`foulfoxhacks:${relative(ROOT, path).replace(/\\/g, "/")}`).digest("hex");

function readGuid(metaPath) {
	const m = /guid: ([0-9a-f]{32})/.exec(readFileSync(metaPath, "utf8"));
	return m ? m[1] : null;
}

function metaBody(path, guid) {
	if (statSync(path).isDirectory()) {
		return `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
	}
	switch (extname(path)) {
		case ".cs":
			return `fileFormatVersion: 2\nguid: ${guid}\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
		case ".asmdef":
			return `fileFormatVersion: 2\nguid: ${guid}\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
		case ".asset":
			return `fileFormatVersion: 2\nguid: ${guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
		case ".json":
		case ".md":
		case ".txt":
			return `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
		default:
			return `fileFormatVersion: 2\nguid: ${guid}\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
	}
}

function walk(dir, out = []) {
	for (const name of readdirSync(dir)) {
		if (name.endsWith(".meta") || name.startsWith(".")) continue;
		const p = join(dir, name);
		out.push(p);
		if (statSync(p).isDirectory()) walk(p, out);
	}
	return out;
}

function ensureMeta(path) {
	const meta = `${path}.meta`;
	if (existsSync(meta)) return readGuid(meta);
	const guid = guidFor(path);
	writeFileSync(meta, metaBody(path, guid));
	console.log("meta  ", relative(ROOT, meta));
	return guid;
}

function header(name, scriptGuid) {
	return `%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${scriptGuid}, type: 3}\n  m_Name: ${name}\n  m_EditorClassIdentifier: \n`;
}

/** Concrete UdonSharpBehaviours in runtime folders need a program asset next to the script. */
function isConcreteUdonBehaviour(csPath) {
	const src = readFileSync(csPath, "utf8");
	if (/abstract\s+class/.test(src)) return false;
	return /class\s+\w+\s*:\s*(UdonSharpBehaviour|FFHVideoTarget)\b/.test(src);
}

for (const pkg of readdirSync(PACKAGES)) {
	const pkgDir = join(PACKAGES, pkg);
	if (!statSync(pkgDir).isDirectory()) continue;
	ensureMeta(pkgDir);

	// UdonSharp assets first, so their .meta files get created by the walk below.
	for (const path of walk(pkgDir)) {
		const inEditor = relative(pkgDir, path).split(/[\\/]/).includes("Editor");
		if (inEditor) continue;
		if (path.endsWith(".cs") && isConcreteUdonBehaviour(path)) {
			const assetPath = path.replace(/\.cs$/, ".asset");
			if (!existsSync(assetPath)) {
				const scriptGuid = ensureMeta(path);
				const name = basename(path, ".cs");
				writeFileSync(
					assetPath,
					header(name, USHARP_PROGRAM_ASSET_SCRIPT) +
						"  serializedUdonProgramAsset: {fileID: 0}\n  udonAssembly: \n  assemblyError: \n" +
						`  sourceCsScript: {fileID: 11500000, guid: ${scriptGuid}, type: 3}\n` +
						"  scriptVersion: 2\n  compiledVersion: 0\n  behaviourSyncMode: 0\n  hasInteractEvent: 0\n  scriptID: 0\n",
				);
				console.log("u#    ", relative(ROOT, assetPath));
			}
		}
		if (path.endsWith(".asmdef")) {
			const assetPath = path.replace(/\.asmdef$/, ".asset");
			if (!existsSync(assetPath)) {
				const asmGuid = ensureMeta(path);
				const name = basename(path, ".asmdef");
				writeFileSync(
					assetPath,
					header(name, USHARP_ASMDEF_ASSET_SCRIPT) + `  sourceAssembly: {fileID: ${ASMDEF_FILE_ID}, guid: ${asmGuid}, type: 3}\n`,
				);
				console.log("u#asm ", relative(ROOT, assetPath));
			}
		}
	}

	for (const path of walk(pkgDir)) ensureMeta(path);
}
console.log("done");
