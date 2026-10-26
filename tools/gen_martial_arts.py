# -*- coding: utf-8 -*-
"""martial_arts.json 생성기. 무공 목록과 카드 이름만 적고, 위력·내력·필요 성취는 등급과 카드 틀에서 계산한다.
실행: python tools/gen_martial_arts.py  →  Assets/StreamingAssets/Data/martial_arts.json
"""
import json, os

R, U, D = "Righteous", "Unorthodox", "Demonic"
F, H, S = "Fast", "Hard", "Soft"

# 카드 틀: a 기본 공격, d 방어, e 회피, m 중급 공격, f 절초
def card(art_id, i, name, slot, style, g, movement):
    if slot == "a":
        kind, power, cost, mastery = "Attack", 8 + 2 * g, 2 + g // 2, 0
    elif slot == "d":
        kind, power, cost, mastery = "Defense", 8 + 2 * g, 2 + g // 2, 0
    elif slot == "e":
        kind, power, cost, mastery = "Evade", 6 + g, 2, 0
    elif slot == "m":
        kind, power, cost, mastery = "Attack", 12 + 2 * g, 4 + g, 3 if g < 3 else 4
    elif slot == "f":
        kind, power, cost, mastery = "Finisher", min(32, 20 + 3 * g), 10 + g, 7
    else:
        raise ValueError(slot)
    return {"id": f"{art_id}_{i}", "name": name, "kind": kind, "style": style, "power": power, "cost": cost, "masteryMin": mastery}

ARTS = []

def art(id, name, type, grade, align, desc, cards=(), faction=None, realm=0, con=0, ins=0):
    a = {"id": id, "name": name, "type": type, "grade": grade, "alignment": align, "description": desc}
    if faction: a["faction"] = faction
    req = {k: v for k, v in (("realmMin", realm), ("constitutionMin", con), ("insightMin", ins)) if v}
    if req: a["requires"] = req
    a["cards"] = [card(id, i + 1, n, slot, st, grade, type == "Movement") for i, (n, slot, st) in enumerate(cards)]
    ARTS.append(a)

# ---- 문파 비전 ----
art("luohan_fist", "나한권", "Fist", 2, R, "소림 제자가 처음 배우는 굳센 권법.", [("나한출동", "a", H), ("금강호체", "d", H), ("나한복호", "m", H), ("십팔나한진", "f", H)], "shaolin")
art("damo_sword", "달마검법", "Sword", 4, R, "달마조사의 뜻을 담은 소림의 검.", [("일위도강", "a", S), ("면벽구년", "d", H), ("선종일검", "m", F), ("달마삼검", "f", H)], "shaolin", realm=2)
art("yijin_jing", "역근경", "Inner", 5, R, "근골을 바꾸는 소림 최고의 심법.", faction="shaolin", realm=3, ins=12)
art("taiji_sword", "태극검법", "Sword", 4, R, "부드러움으로 강함을 누르는 무당의 검.", [("운중월", "a", S), ("태극원환", "d", S), ("칠성연환", "m", F), ("태극혜검", "f", S)], "wudang", realm=2, ins=12)
art("taiji_fist", "태극권", "Fist", 3, R, "힘을 흘려 되돌리는 무당의 권.", [("남작미", "a", S), ("운수", "d", S), ("단편", "m", S), ("태극일기", "f", S)], "wudang")
art("chunyang_gong", "순양무극공", "Inner", 4, R, "맑은 양기를 쌓는 무당의 심법.", faction="wudang", realm=1)
art("plum_sword", "매화검법", "Sword", 4, R, "스물네 송이 매화를 피워 내는 화산의 검.", [("매화점점", "a", F), ("암향부동", "d", S), ("매화분분", "m", F), ("이십사수매화", "f", F)], "huashan", realm=2)
art("zixia_gong", "자하신공", "Inner", 4, R, "자줏빛 노을처럼 피어오르는 화산의 심법.", faction="huashan", realm=1, ins=10)
art("jinding_sword", "금정검법", "Sword", 3, R, "아미산 금정의 빛을 닮은 맑은 검.", [("금정조일", "a", F), ("불광보조", "d", S), ("아미월영", "m", S), ("금정만광", "f", F)], "emei")
art("yunlong_steps", "운룡대팔식", "Movement", 4, R, "구름 속 용처럼 몸을 꺾는 곤륜의 신법.", [("운룡번신", "e", F), ("용행구천", "e", S), ("운룡탐조", "a", F), ("신룡출운", "f", F)], "kunlun")
art("sunset_sword", "사일검법", "Sword", 3, R, "해를 쏘아 떨어뜨린다는 점창의 쾌검.", [("사일", "a", F), ("유성낙지", "m", F), ("점창운무", "d", F), ("구일추락", "f", F)], "diancang")
art("qingcheng_sword", "청성검법", "Sword", 3, R, "청성산 푸른 숲처럼 촘촘한 검.", [("송풍", "a", F), ("청성낙엽", "d", S), ("무영자", "m", F), ("청성일검", "f", F)], "qingcheng")
art("thirtysix_sword", "천하삼십육검", "Sword", 3, R, "종남의 서른여섯 갈래 정검.", [("천하제일식", "a", H), ("종남중검", "d", H), ("삼십육로", "m", H), ("천하귀일", "f", H)], "zhongnan")
art("seven_wounds", "칠상권", "Fist", 3, R, "한 주먹에 일곱 갈래 경력을 싣는 공동의 권.", [("일상", "a", H), ("칠상호체", "d", H), ("삼상연격", "m", H), ("칠상귀원", "f", H)], "kongtong", con=10)
art("dragon_palms", "항룡십팔장", "Fist", 5, R, "개방 방주에게만 전하는 천하제일 장법.", [("견룡재전", "a", H), ("비룡재천", "d", H), ("항룡유회", "m", H), ("항룡십팔장 종식", "f", H)], "beggars", realm=3, con=12)
art("dog_staff", "타구봉법", "Spear", 4, R, "개를 패는 막대로 천하를 누른다는 개방의 봉법.", [("반", "a", S), ("전", "d", S), ("도", "m", F), ("천하무구", "f", S)], "beggars", realm=2)
art("skyline_sword", "창궁무애검", "Sword", 4, R, "하늘 끝까지 막힘이 없다는 남궁세가의 검.", [("창궁일섬", "a", F), ("무애호신", "d", H), ("창궁낙뢰", "m", H), ("창궁무애", "f", H)], "namgung_clan", realm=2)
art("starshift", "두전성이", "Fist", 4, R, "상대의 힘으로 상대를 치는 모용세가의 절기.", [("이형환위", "d", S), ("성이", "a", S), ("두전", "m", S), ("이피지도환시피신", "f", S)], "murong_clan", realm=2, ins=12)
art("zhuge_devices", "제갈신기", "Hidden", 3, R, "진법과 기관을 품은 제갈세가의 암기술.", [("팔진암전", "a", F), ("기문둔갑", "e", S), ("연노십발", "m", F), ("팔진도", "f", F)], "zhuge_clan", ins=11)
art("rain_of_petals", "만천화우", "Hidden", 4, R, "하늘 가득 꽃비처럼 쏟아지는 당가의 암기.", [("비황석", "a", F), ("독질려", "m", F), ("수리검막", "d", F), ("만천화우", "f", F)], "tang_clan", realm=1)
art("five_tiger_saber", "오호단문도", "Saber", 4, R, "다섯 호랑이의 기세로 문을 끊는 팽가의 도.", [("맹호출림", "a", H), ("호보", "d", H), ("오호쟁식", "m", H), ("오호단문", "f", H)], "paeng_clan", realm=1, con=11)
art("greenwood_saber", "녹림도법", "Saber", 3, U, "산채에서 갈고닦은 거친 도법.", [("산적출몰", "a", H), ("막고베기", "d", H), ("녹림쇄골", "m", H), ("녹림대참", "f", H)], "nokrim_hq")
art("haomun_stealth", "하오은신술", "Movement", 2, U, "그림자에 몸을 숨기는 하오문의 비술.", [("암영", "e", F), ("은신", "e", S), ("등뒤찌르기", "a", F), ("하오일격", "f", F)], "haomun")
art("water_flight", "수상비", "Movement", 2, U, "물 위를 걷는 장강수로채의 신법.", [("답랑", "e", F), ("물살타기", "e", S), ("수중검", "a", F), ("장강도해", "f", F)], "yangtze_pirates")
art("heavenly_demon_art", "천마신공", "Inner", 5, D, "천마신교 교주에게만 전하는 마공의 정점.", faction="heavenly_demon", realm=3)
art("demon_steps", "천마군림보", "Movement", 4, D, "한 걸음마다 땅을 울리는 천마의 보법.", [("군림", "e", H), ("마보", "e", S), ("천마진각", "a", H), ("천마군림", "f", H)], "heavenly_demon", realm=2)
art("demon_sword", "천마검법", "Sword", 5, D, "핏빛 검기로 하늘을 가르는 천마의 검.", [("마검출수", "a", H), ("혈막", "d", H), ("천마혈검", "m", F), ("천마멸세", "f", H)], "heavenly_demon", realm=3)
art("blood_demon_art", "혈마공", "Inner", 4, D, "남의 피로 내공을 쌓는 혈교의 마공.", faction="blood_cult", realm=1)
art("blood_shadow_steps", "혈영신법", "Movement", 3, D, "핏빛 그림자만 남기는 혈교의 신법.", [("혈영", "e", F), ("잔영", "e", S), ("혈조", "a", F), ("혈영만상", "f", F)], "blood_cult")
art("taishan_eighteen", "태산십팔반", "Saber", 3, R, "태산처럼 무거운 열여덟 갈래 병기술.", [("태산압정", "a", H), ("반석", "d", H), ("오악독존", "m", H), ("태산붕괴", "f", H)], "taishan")
art("songyang_sword", "대숭양검법", "Sword", 3, R, "숭산 봉우리처럼 곧고 높은 검.", [("숭양일검", "a", H), ("숭산고정", "d", H), ("천외삼봉", "m", F), ("대숭양신검", "f", H)], "songshan")
art("wild_goose_sword", "회풍락안검", "Sword", 3, R, "바람을 돌려 기러기를 떨군다는 형산의 검.", [("회풍", "a", F), ("락안", "m", F), ("형산운무", "d", S), ("백운출수", "f", F)], "hengshan")
art("vajra_gong", "금강부동신공", "Inner", 3, R, "흔들림 없는 오대산 불문의 심법.", faction="wutai")
art("changbai_sword", "장백검법", "Sword", 3, R, "눈보라를 가르는 장백파의 검.", [("설풍", "a", F), ("빙벽", "d", H), ("천지설검", "m", F), ("장백일휘", "f", F)], "changbai")
art("southern_fist", "소림남권", "Fist", 3, R, "남소림의 짧고 매서운 권.", [("호권", "a", H), ("학권", "d", S), ("오형연환", "m", H), ("남권진산", "f", H)], "south_shaolin")
art("wuyi_sword", "무이검법", "Sword", 2, R, "아홉 굽이 계곡을 닮은 무이파의 검.", [("구곡", "a", S), ("계류", "d", S), ("무이연봉", "m", F), ("구곡귀일", "f", S)], "wuyi")
art("yiyang_finger", "일양지", "Finger", 4, R, "한 줄기 양기를 손끝에 모으는 대리단가의 지공.", [("일양점혈", "a", F), ("양기호체", "d", S), ("일양관일", "m", F), ("일양지 극의", "f", F)], "dan_clan", realm=1, ins=12)
art("five_poison_palm", "오독장", "Fist", 3, D, "다섯 독을 손바닥에 품은 오독문의 장법.", [("독수", "a", S), ("독무", "d", S), ("오독연장", "m", H), ("오독귀원", "f", S)], "five_poisons")
art("myriad_poison_art", "만독심법", "Inner", 4, D, "만 가지 독을 몸에 녹이는 만독문의 심법.", faction="myriad_poison", realm=1)
art("ice_soul_art", "빙백신공", "Inner", 4, D, "뼈까지 얼리는 북해빙궁의 심법.", faction="north_ice_palace", realm=1)
art("holy_fire_art", "성화신공", "Inner", 4, D, "성스러운 불을 몸에 일으키는 명교의 심법.", faction="myeonggyo", realm=1)
art("white_lotus_art", "백련심법", "Inner", 3, D, "흰 연꽃 아래 마음을 묶는 백련교의 심법.", faction="white_lotus")

# ---- 일반 무공 (누구나) ----
art("three_talent_sword", "삼재검법", "Sword", 1, R, "하늘·땅·사람 세 자세로 된 기본 검법.", [("천", "a", F), ("지", "d", H), ("인", "m", S), ("삼재합일", "f", H)])
art("taizu_fist", "태조장권", "Fist", 1, R, "어느 무관에서나 가르치는 기본 권법.", [("직권", "a", H), ("막기", "d", H), ("연타", "m", F), ("장권일격", "f", H)])
art("tiger_saber", "오호도법", "Saber", 1, R, "표사와 낭인이 즐겨 쓰는 투박한 도법.", [("내려베기", "a", H), ("도배막기", "d", H), ("호랑이발톱", "m", H), ("오호일도", "f", H)])
art("escort_spear", "표국창법", "Spear", 1, R, "표국에서 전해지는 실전 창법.", [("찌르기", "a", F), ("창대막기", "d", H), ("회창", "m", S), ("표국일창", "f", F)])
art("ronin_sword", "낭인검법", "Sword", 2, R, "떠돌이 검객들이 싸우며 익힌 검.", [("기습", "a", F), ("물러서기", "e", F), ("발검", "m", F), ("낭인일섬", "f", F)])
art("flying_dagger", "비도술", "Hidden", 1, R, "단도를 던져 맞히는 기초 암기술.", [("비도", "a", F), ("몸굴리기", "e", F), ("쌍비도", "m", F), ("연환비도", "f", F)])
art("basic_breathing", "기초토납법", "Inner", 1, R, "숨을 고르며 기를 모으는 가장 기초적인 심법.")
art("hunyuan_gong", "혼원공", "Inner", 2, R, "흩어진 기를 하나로 묶는 심법.")
art("breeze_sword", "청풍검법", "Sword", 2, R, "맑은 바람처럼 가벼운 검법.", [("청풍", "a", F), ("유수", "d", S), ("청풍명월", "m", S), ("청풍만리", "f", F)])
art("six_harmony_fist", "육합권", "Fist", 2, R, "몸과 마음 여섯을 하나로 모으는 권법.", [("육합직권", "a", H), ("육합보", "d", H), ("육합연환", "m", H), ("육합귀일", "f", H)])
art("mountain_saber", "개산도법", "Saber", 2, R, "산도 가른다는 묵직한 도법.", [("개산", "a", H), ("횡도", "d", H), ("벽산", "m", H), ("개천벽지", "f", H)])
art("grass_flight", "초상비", "Movement", 1, R, "풀잎 위를 스치는 가벼운 신법.", [("초상비", "e", F), ("부평초", "e", S), ("비각", "a", F), ("만리추풍", "f", F)])
art("snow_steps", "답설무흔", "Movement", 3, R, "눈 위를 걸어도 자국이 없는 신법.", [("무흔", "e", F), ("부운", "e", S), ("답설", "a", F), ("무흔만리", "f", F)])
art("flick_finger", "탄지신통", "Finger", 3, R, "손가락을 튕겨 기를 쏘는 지공.", [("탄지", "a", F), ("지풍호신", "d", S), ("연환탄지", "m", F), ("탄지신통", "f", F)], ins=11)
art("vajra_palm", "대력금강장", "Fist", 3, R, "금강의 힘으로 내리치는 장법.", [("금강장", "a", H), ("금강신", "d", H), ("대력연장", "m", H), ("금강파산", "f", H)], con=11)
art("void_sword", "태허검법", "Sword", 4, R, "이름 없는 고수가 남긴 허공의 검.", [("허초", "a", S), ("태허", "d", S), ("허실상생", "m", F), ("태허귀일", "f", S)], realm=2, ins=13)
art("free_mind", "소요심법", "Inner", 3, R, "얽매임 없이 기를 기르는 심법.", ins=10)

# ---- 사·마 일반 무공 ----
art("black_palm", "흑살장", "Fist", 2, U, "검은 기운으로 내장을 상하게 하는 장법.", [("흑살", "a", H), ("흑막", "d", S), ("흑살연장", "m", H), ("흑살천라", "f", H)])
art("viper_sword", "독사검", "Sword", 2, U, "독사처럼 틈을 파고드는 검.", [("사신", "a", F), ("사행", "e", F), ("독아", "m", F), ("독사출동", "f", F)])
art("bone_breaker", "쇄골수", "Fist", 3, U, "뼈를 부수는 사파의 금나수.", [("쇄골", "a", H), ("금나", "d", S), ("분근착골", "m", H), ("쇄골만상", "f", H)])
art("blood_wolf_saber", "혈랑도법", "Saber", 3, U, "피 냄새를 쫓는 이리 떼의 도법.", [("혈랑", "a", F), ("이빨막기", "d", H), ("군랑", "m", F), ("혈랑광풍", "f", F)])
art("shadow_dagger", "은형비도", "Hidden", 2, U, "보이지 않게 날아드는 비도.", [("은형", "a", F), ("숨기", "e", S), ("쌍은형", "m", F), ("은형만천", "f", F)])
art("soul_snare", "섭혼술", "Finger", 3, D, "눈빛으로 넋을 홀리는 마공.", [("섭혼", "a", S), ("환영", "e", S), ("탈혼", "m", S), ("섭혼대법", "f", S)], ins=10)
art("star_absorb", "흡성대법", "Inner", 4, D, "남의 내공을 빨아들이는 금단의 마공.", realm=1)
art("blood_hand", "혈수장", "Fist", 4, D, "손바닥을 핏빛으로 물들이는 마공.", [("혈수", "a", H), ("혈막", "d", H), ("혈수천라", "m", H), ("혈해무변", "f", H)], realm=1)
art("corpse_blood_art", "시혈마공", "Inner", 3, D, "죽은 자의 피로 기를 기르는 마공.")
art("demon_spirit_sword", "마령검", "Sword", 3, D, "검에 깃든 원혼이 우는 마검.", [("마령", "a", F), ("귀막", "d", S), ("원혼검", "m", F), ("만귀곡", "f", F)])

NARRATION = {
    "open": [
        "{a:와} {b:이} 서로 포권을 하고 자세를 잡았다.",
        "{a:이} 먼저 {b}의 빈틈을 노렸다.",
        "두 사람 사이로 바람 한 줄기가 지나갔다. {a:이} 먼저 움직였다.",
        "{b:은} 말없이 {art}의 기수식을 취했고, {a:은} 숨을 골랐다.",
        "{a:와} {b:은} 열 걸음을 사이에 두고 마주 섰다.",
    ],
    "hit": [
        "{a}의 {art} {card}에 {b:이} 크게 휘청였다.",
        "{a:이} {card:으로} {b}의 어깨를 베었다.",
        "{card:이} {b}의 가슴팍에 꽂혔다.",
        "{a}의 {card:이} 정확히 {b}의 옆구리를 파고들었다.",
        "{b:은} {card:을} 미처 보지 못하고 그대로 맞았다.",
        "{a:은} {card:으로} {b:을} 몰아붙였다.",
        "{card:을} 맞은 {b}의 입가에 피가 비쳤다.",
    ],
    "graze": [
        "{b:은} 몸을 틀었지만 {card}의 끝에 옷자락이 찢겼다.",
        "{b:은} 반 박자 늦게 피했고, {card:이} 팔을 스쳤다.",
        "{card:이} {b}의 뺨을 스치며 핏줄기를 남겼다.",
        "{b:은} 가까스로 물러났으나 {card}의 여파까지 피하지는 못했다.",
    ],
    "block": [
        "{b:은} {guard:으로} {a}의 {card:을} 받아냈다.",
        "{a}의 {card:은} {b}의 {guard}에 막혀 힘을 잃었다.",
        "{b:은} {guard:으로} 굳게 버텨 한 걸음도 물러서지 않았다.",
        "쇳소리가 울렸다. {guard:이} {card:을} 정면으로 막아섰다.",
    ],
    "evade": [
        "{b:은} {guard:으로} 바람처럼 몸을 빼 {card:을} 흘려보냈다.",
        "{card:이} 허공을 갈랐다. {b:은} 이미 그 자리에 없었다.",
        "{b:은} {guard:으로} 한 발 비켜서 {card:을} 피했다.",
        "{b}의 신형이 흐릿해지더니 {card:이} 빗나갔다.",
    ],
    "clash": [
        "두 사람의 초식이 정면으로 부딪쳐 불꽃이 튀었다.",
        "{card:와} 상대의 초식이 맞부딪쳐 둘 다 뒤로 밀려났다.",
        "같은 기세가 맞부딪쳤다. 둘 다 손목이 저려 왔다.",
        "{a:와} {b}의 초식이 한데 엉켜 굉음이 울렸다.",
    ],
    "probe": [
        "두 사람은 서로 빈틈을 노리며 거리를 쟀다.",
        "누구도 먼저 나서지 않았다. 숨 막히는 탐색이 이어졌다.",
        "{a:와} {b:은} 원을 그리며 돌았다.",
        "잠시 손을 멈춘 두 사람이 내력을 고르며 서로를 노려보았다.",
    ],
    "counter": [
        "{card:은} 상성이 맞아 {b}의 {guard:을} 깨뜨렸다.",
        "{a:은} {guard}의 흐름을 읽고 그 틈으로 {card:을} 밀어 넣었다.",
        "{b}의 {guard:이} 무너졌다. {card:이} 그 상성을 꿰뚫었다.",
        "{guard:은} {card} 앞에서 종이처럼 찢겼다.",
    ],
    "finisher": [
        "{a:이} 숨을 고르더니 절초 {card:을} 펼쳤다!",
        "{a}의 기세가 한순간에 바뀌었다. 절초 {card}였다!",
        "{a:이} 남은 내력을 끌어모아 {card:을} 날렸다!",
        "주위가 고요해졌다. {a}의 손에서 {card:이} 피어났다!",
    ],
    "exhausted": [
        "내력이 바닥난 {a:은} 숨을 헐떡였다.",
        "{a:은} 내력이 다해 초식을 잇지 못했다.",
        "{a}의 손끝이 떨렸다. 더 쓸 내력이 없었다.",
    ],
    "win": [
        "{loser:은} 무릎을 꿇었다. {winner}의 승리였다.",
        "{winner:이} 검을 거두었다. 승부는 끝났다.",
        "{loser:은} 더 버티지 못하고 쓰러졌다. {winner:이} 이겼다.",
    ],
    "death": [
        "{loser:은} 그 자리에서 숨을 거두었다.",
        "{loser:은} 다시는 일어나지 못했다.",
        "쓰러진 {loser}의 숨이 그대로 끊겼다.",
    ],
    "timeout": [
        "승부가 나지 않아 두 사람은 물러섰다. 판정은 {winner}의 우세였다.",
        "끝내 결판은 나지 않았으나, 지켜보던 이들은 {winner:이} 앞섰다고 보았다.",
    ],
}

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    path = os.path.join(root, "Assets", "StreamingAssets", "Data", "martial_arts.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"arts": ARTS, "narration": NARRATION}, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"{len(ARTS)} arts -> {path}")

if __name__ == "__main__":
    main()
