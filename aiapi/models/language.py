from enum import Enum


class Language(Enum):
    En = "En"
    De = "De"
    
    def invert(self):
        return type(self).En if self == type(self).De else type(self).De
    
    def __str__(self) -> str:
        return "English" if self == type(self).En else "German"
