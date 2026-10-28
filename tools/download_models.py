"""Download the official PaddleOCR inference models and their matching dictionaries.

Run from any directory: python tools/download_models.py
Requires curl on PATH. Only inference files are extracted; training weights are excluded.
"""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1] / "模型"
BOS = "https://paddleocr.bj.bcebos.com/"
RAW = "https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/release/2.7/"
LANGUAGES = {
    "Chinese": ("ch_PP-OCRv4_rec_infer", "PP-OCRv4/chinese/", "ppocr/utils/ppocr_keys_v1.txt"),
    "Korean": ("korean_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/korean_dict.txt"),
    "Japanese": ("japan_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/japan_dict.txt"),
    "Latin": ("latin_PP-OCRv3_rec_infer", "PP-OCRv3/multilingual/", "ppocr/utils/dict/latin_dict.txt"),
    "Arabic": ("arabic_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/arabic_dict.txt"),
    "Cyrillic": ("cyrillic_PP-OCRv3_rec_infer", "PP-OCRv3/multilingual/", "ppocr/utils/dict/cyrillic_dict.txt"),
    "Devanagari": ("devanagari_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/devanagari_dict.txt"),
    "Telugu": ("te_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/te_dict.txt"),
    "Kannada": ("ka_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/ka_dict.txt"),
    "Tamil": ("ta_PP-OCRv4_rec_infer", "PP-OCRv4/multilingual/", "ppocr/utils/dict/ta_dict.txt"),
}


def download(url, destination):
    subprocess.run(["curl.exe", "-sSL", "--fail", "--retry", "3", "--connect-timeout", "30",
                    "--max-time", "600", url, "-o", str(destination)], check=True)


def model(url):
    name = url.rsplit("/", 1)[1].removesuffix(".tar")
    target = ROOT / name
    target.mkdir(parents=True, exist_ok=True)
    if all((target / filename).is_file() and (target / filename).stat().st_size > 0
           for filename in ("inference.pdmodel", "inference.pdiparams")):
        return
    with tempfile.TemporaryDirectory(dir=ROOT) as temp:
        archive = Path(temp) / "model.tar"
        download(url, archive)
        with tarfile.open(archive) as tar:
            for member in tar.getmembers():
                # Flatten only the known inference artifacts; never extract archive paths.
                filename = Path(member.name).name
                if member.isfile() and filename in ("inference.pdmodel", "inference.pdiparams", "inference.pdiparams.info"):
                    (target / filename).write_bytes(tar.extractfile(member).read())
    for filename in ("inference.pdmodel", "inference.pdiparams"):
        if not (target / filename).is_file():
            raise RuntimeError(f"Missing {target / filename}")
    print(f"Downloaded {name}", flush=True)


def dictionary(path):
    target = ROOT / "keys" / Path(path).name
    if target.is_file() and target.stat().st_size > 0:
        return
    # CDN serves the same release branch when raw.githubusercontent.com is unreliable.
    try:
        download("https://cdn.jsdelivr.net/gh/PaddlePaddle/PaddleOCR@release/2.7/" + path, target)
    except subprocess.CalledProcessError:
        download(RAW + path, target)
    if not target.read_text(encoding="utf-8").splitlines():
        raise RuntimeError(f"Empty dictionary: {target}")


def main():
    (ROOT / "keys").mkdir(parents=True, exist_ok=True)
    models = [BOS + "PP-OCRv4/chinese/ch_PP-OCRv4_det_infer.tar",
              BOS + "dygraph_v2.0/ch/ch_ppocr_mobile_v2.0_cls_infer.tar"]
    models += [BOS + folder + name + ".tar" for name, folder, _ in LANGUAGES.values()]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        tasks = [pool.submit(model, url) for url in models]
        tasks += [pool.submit(dictionary, path) for _, _, path in LANGUAGES.values()]
        for task in tasks:
            task.result()
    manifest = {"source": "PaddlePaddle/PaddleOCR release/2.7 paddleocr.py MODEL_URLS",
                "languages": {lang: {"recognition": name, "url": BOS + folder + name + ".tar",
                                      "dictionary": "keys/" + Path(path).name, "dictionary_url": RAW + path,
                                      "rec_img_h": 48} for lang, (name, folder, path) in LANGUAGES.items()},
                "shared_models": models[:2],
                "sha256": {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest()
                           for p in ROOT.rglob("*") if p.is_file() and p.name != "manifest.json"}}
    (ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
