from dataclasses import dataclass


@dataclass
class ChatInfo:
    id: int
    gist_id_last_sent: int
