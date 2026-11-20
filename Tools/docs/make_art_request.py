# 이전 명령 호환. 공통 목록은 request_data.py예요.
from make_requests import inventory, make_team_pdf

if __name__ == "__main__":
    make_team_pdf("아트", inventory())
