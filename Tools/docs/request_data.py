"""요청 현황과 팀 PDF의 공통 목록. 상태는 파일 존재 여부가 아니라 수령 기록으로 정해요.

그림 출처는 art_tree_data.TREE, 물건 사용 여부는 Assets/Data의 GUID 참조에서 읽어요.
새 그림을 받으면 TREE의 출처를 아트팀으로 바꾸고, 아래 신규 요청도 함께 갱신해요.
"""
from dataclasses import dataclass
from pathlib import Path
import re

from PIL import Image

import paths
from art_tree_data import TREE, STAGES

ROOT = Path(paths.ROOT)
SPRITES = ROOT / "Assets/Sprites"
SOUND = ROOT / "Assets/Resources/Sound"


@dataclass(frozen=True)
class Request:
    team: str
    group: str
    file: str                 # Assets 안 상대 경로. 신규 요청은 제안 이름이에요.
    status: str               # 받음 / 임시 / 없음
    method: str
    where: str
    note: str
    action: str               # 완료 / 교체 / 신규 / 유지
    evidence: str
    spec: str = ""


def art_inventory():
    entries = {}
    for screen, _, rows in TREE:
        for obj, rel, source in rows:
            if "/" not in rel or "아래" in rel or "지금은 안 씀" in obj or "아직 안 씀" in obj or "숨김" in obj:
                continue
            rel = rel.replace("UI/../", "") + ".png"
            if not (SPRITES / rel).is_file():
                raise ValueError(f"그림 파일 없음: {rel}")
            if rel not in entries:
                entries[rel] = [source, []]
            entries[rel][1].append(f"{screen} / {obj}")

    # 옛 O/X 조합 그림 중 현재 물건 데이터에서 쓰는 것만 요청해요.
    used = set(re.findall(r"guid: ([0-9a-f]{32})", "\n".join(
        p.read_text(encoding="utf-8") for p in (ROOT / "Assets/Data").glob("*.asset"))))
    for _, folder, key, _, _, origin in STAGES:
        for p in sorted((SPRITES / folder).glob("*.png")):
            guid = re.search(r"^guid: (\w+)", Path(str(p) + ".meta").read_text(encoding="utf-8"), re.M).group(1)
            if guid in used:
                entries[p.relative_to(SPRITES).as_posix()] = [
                    origin if p.stem == key + "_Original" else "Codex 임시 그림",
                    [f"1챕터 {key} 물건 데이터"]]

    out = []
    for rel, (source, places) in sorted(entries.items()):
        group = ("2~4챕터 물건·카드" if re.match(r"Chapter[234]/", rel) else
                 "5챕터 그림" if rel.startswith("Chapter5/") else
                 "도깨비 머리 불 색" if "/Mood/" in rel else
                 "방 배경" if "BG_Room" in rel and not rel.endswith("BG_Room.png") else
                 "1챕터 물건" if rel.startswith("Stage") else "공통 그림")
        received = "아트팀" in source or "기획서" in source or "기획 단계" in source
        method = source
        if "/Mood/" in rel or "BG_RoomRain" in rel or "BG_RoomEvening" in rel or "BG_RoomNight" in rel:
            method = "기존 아트 색 바꾸기 (Tools/art_temp)"
        elif "Char_Dokkaebi_Dark" in rel:
            method = "Codex CLI가 기존 아트 색 바꾸기 (finish_chapter5.ps1)"
        elif "Char_FriendFace" in rel:
            method = "Claude가 아트팀 친구 원본(Friend_Original)에서 얼굴만 오림. 임의 제작 (A3, 11/23)"
        elif "Char_Friend_Spirit" in rel:
            method = "Codex CLI가 친구 원본을 흐리게 가공 (finish_chapter5.ps1)"
        elif "Codex" in source:
            method = "Codex 임시 그림 (art_tree_data 기록)"
        action = "완료" if received else "유지" if source.startswith("코드로") and not rel.startswith("Chapter4/") else "교체"
        note = "현재 그림 유지. 팀에 새로 요청하지 않아요." if action == "유지" else "현재 그림과 비율·투명 영역을 맞춰 교체해 주세요."
        if re.search(r"Ch[2345]_.*_(Original|Wrong\d)", rel):
            note += " 원본의 판정 기준과 각 오답의 틀린 특징을 유지해 주세요. 게임 플로우의 물건 표를 함께 봐 주세요."
        if rel.endswith(("Dialogue_Box.png", "Draw_Paper.png", "Paper_Score.png", "Chapter_Picnic.png")):
            method, note = "아트팀 수령 (2026-11-17, C-22·C-25)", "게임 연결 완료. 재요청하지 않아요."
        if rel.endswith(("HUD_CandleGauge.png", "HUD_CandleFlame.png")):
            method, note = "아트팀 촛대 1장 수령 (2026-11-18, C-49) 후 분리", "막대·불꽃은 같은 원본에서 나눈 파일. 재요청하지 않아요."
        with Image.open(SPRITES / rel) as image:
            spec = f"{image.width}×{image.height}px PNG"
        out.append(Request("아트", group, "Sprites/" + rel, "받음" if received else "임시", method,
                           " / ".join(dict.fromkeys(places)), note, action, "art_tree_data.TREE · Assets/Data", spec))
    return out


def new_art(stem, where, note, evidence, template):
    with Image.open(SPRITES / template) as im:
        spec = f"{im.width}×{im.height}px 기준, 투명 PNG (제안)"
    chapter = "Chapter4" if "Awkward" in stem else "Chapter5"
    return Request("아트", "아직 없는 그림", f"Sprites/{chapter}/{stem}.png", "없음", "미제작",
                   where, note, "신규", evidence, spec)


def missing_art():
    body = "Characters/Char_Dokkaebi_Normal.png"
    face = "Characters/Char_DokkaebiFace_Normal.png"
    dark = "Chapter5/Char_Dokkaebi_Dark.png"
    specs = [
        ("Char_Dokkaebi_Awkward", "4챕터 몸", "붉은 머리 불, 어색한 웃음. 기존 몸과 구도 맞추기.", "기획서 4장 · C-55", body),
        ("Char_DokkaebiFace_Awkward", "4챕터 대사 얼굴", "몸과 같은 어색한 웃음·머리 불 색.", "기획서 4장 · C-55", face),
        ("Char_Dokkaebi_DarkThink", "5챕터 판정 대기", "초록 도깨비 대신 흑화 도깨비의 판정 대기 자세.", "BACKLOG B2", dark),
        ("Char_Dokkaebi_DarkRun", "5챕터 해피 엔딩", "도망가는 자세. 이동·퇴장은 개발이 연결해요.", "BACKLOG B5", dark),
        ("Char_Friend_SpiritEscape", "5챕터 친구 영혼 이탈", "흐리게 만든 정지 그림은 이미 있어요. 여기서는 몸에서 빠져나가는 영혼용 별도 그림을 요청해요. 애니메이션 연결은 개발 작업.", "BACKLOG C4 · 사용자 승인 11/20", "Chapter5/Char_Friend_Spirit.png"),
    ]
    specs += [(f"Char_DokkaebiFace_Dark{pose}", "5챕터 흑화 대사 얼굴", "기존 흑화 전신과 같은 그림체·머리 색. 초록 얼굴을 대신해요.", "BACKLOG B1", face)
              for pose in ("Normal", "Proud", "Tease")]
    return [new_art(*spec) for spec in specs]


# (파일 이름, 나오는 때, 느낌, 권장 길이). 기존 17개는 모두 VARCO 임시본이에요.
SOUND_SPECS = [
    ("bgm_room", "방 대화·관찰", "포근한 아침, 아기자기하고 느리게", "30~60초 반복"),
    ("bgm_draw", "그리기", "집중할 수 있게 작고 잔잔한 똑딱거림", "30~60초 반복"),
    ("bgm_judge", "판정 대기·결과·채점표", "바순·클라리넷으로 갸웃갸웃, 성공·실패 모두 어울리게", "20~40초 반복"),
    ("bgm_picnic", "1챕터 소풍 엔딩", "밝지만 시끄럽지 않게", "30~60초 반복"),
    ("sfx_tap", "대사·버튼 터치", "가벼운 톡", "0.1초"),
    ("sfx_count", "관찰·돋보기 숫자", "짧은 똑딱", "0.2초"),
    ("sfx_submit", "다 그렸어!", "종이를 접는 소리", "0.5초"),
    ("sfx_give", "쪽지 건네기", "가볍게 휙", "0.6초"),
    ("sfx_item", "물건 등장", "뿅", "0.3초"),
    ("sfx_success", "성공 결과", "밝은 짠, sfx_item과 겹쳐도 어울리게", "1초"),
    ("sfx_fail", "실패 결과", "어이없는 띠용", "1초"),
    ("sfx_mark", "채점표", "연필로 슥슥", "최대 3초"),
    ("sfx_cheer", "1챕터 엔딩 시작", "작은 환호", "2초"),
    ("sfx_talk", "글자 한 자씩 표시", "자주 반복돼도 거슬리지 않는 작은 틱", "0.05초 이하"),
    ("sfx_magnifier", "돋보기 사용", "렌즈가 반짝 켜짐", "0.6초"),
    ("sfx_snack_in", "사탕·무료 돋보기 등장", "샤샤샥", "0.7초"),
    ("sfx_land", "소풍 물건 착지", "가벼운 톡", "0.2초"),
]
NEW_SOUND = [
    ("bgm_curse", "5챕터 숲·흑화 이후", "밝은 방 음악을 대신할 불안한 분위기. 과도한 음량 없이", "30~60초 반복", "BACKLOG D1"),
    ("sfx_scare", "5챕터 점프스케어", "짧게 놀라게 하되 과도한 음량 없이", "0.5~1초", "BACKLOG B3 · 11/20 승인"),
    ("sfx_heart_loss", "5챕터 주인공 하트 감소", "한 개 깎이는 순간을 알림", "0.5~1초", "BACKLOG C2"),
    ("sfx_interfere", "그리기 방해", "반복해도 귀에 거슬리지 않는 장난·위협", "0.5~1초", "BACKLOG C3"),
    ("sfx_ending_happy", "5챕터 해피 엔딩", "저주가 풀리는 안도감", "2~4초", "BACKLOG C6"),
    ("sfx_gameover", "5챕터 게임오버", "위협·좌절, 다른 엔딩과 구별", "2~4초", "BACKLOG C6"),
    ("amb_rain", "2챕터 비 오는 방", "대사를 가리지 않는 빗소리", "20~40초 반복", "C-55 · 11/20 승인"),
    ("bgm_evening", "3챕터 저녁 방", "기존 악기 톤을 유지하며 불안한 복선", "30~60초 반복", "C-55 · 11/20 승인"),
    ("bgm_night", "4챕터 밤 방", "어둡고 불편한 분위기, 5챕터로 연결", "30~60초 반복", "C-55 · 11/20 승인"),
]


def sound_inventory():
    code = (ROOT / "Assets/Scripts/Sfx.cs").read_text(encoding="utf-8")
    constants = set(re.findall(r'const string \w+ = "((?:sfx|bgm)_\w+)"', code))
    documented = {s[0] for s in SOUND_SPECS}
    existing = {p.stem for p in SOUND.iterdir() if p.suffix.lower() in (".wav", ".ogg", ".mp3")}
    if constants != documented or existing != documented:
        raise ValueError(f"소리 목록 갱신 필요: 코드 {constants ^ documented}, 파일 {existing ^ documented}")
    out = []
    for name, where, feel, length in SOUND_SPECS:
        p = next(p for p in SOUND.iterdir() if p.stem == name and p.suffix in (".wav", ".ogg", ".mp3"))
        out.append(Request("사운드", "기존 소리 교체", "Resources/Sound/" + p.name, "임시", "VARCO Sound (11/11 효과음, 11/12~13 배경음)",
                           where, feel, "교체", "C-21·C-28 · ArtSource/Sound/varco/출처.txt · Sfx.cs", length))
    for name, where, feel, length, evidence in NEW_SOUND:
        out.append(Request("사운드", "아직 없는 소리", f"Resources/Sound/{name}.ogg" if name.startswith(("bgm", "amb")) else f"Resources/Sound/{name}.wav",
                           "없음", "미제작", where, feel, "신규", evidence, length + " (제안)"))
    return out


def inventory():
    rows = art_inventory() + missing_art() + sound_inventory()
    names = [r.file for r in rows]
    if len(names) != len(set(names)):
        raise ValueError("요청 파일 이름이 중복돼요")
    for r in rows:
        exists = (ROOT / "Assets" / r.file).is_file()
        if exists == (r.status == "없음"):
            raise ValueError(f"파일과 수령 상태를 다시 확인해 주세요: {r.file} ({r.status})")
    return rows
