import { Tooltip, Typography } from "@mui/material"
import { useEffect, useRef, useState } from "react";

export const ShortenedTypography = ({ value }: { value: string }) => {
  const typographyRef = useRef<HTMLDivElement>(null);
  const [isOverflowing, setIsOverflowing] = useState(false);

  useEffect(() => {
    const checkOverflow = () => {
      if (typographyRef.current) {
        setIsOverflowing(typographyRef.current.scrollWidth > typographyRef.current.clientWidth);
      }
    };

    checkOverflow();
    window.addEventListener("resize", checkOverflow);

    return () => {
      window.removeEventListener("resize", checkOverflow);
    };
  }, [value]);

  const typography = <Typography 
    ref={ typographyRef }
    sx={{ 
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
  >
    { value }
  </Typography>;

  return isOverflowing 
    ? <Tooltip title={ value }>
        { typography }
      </Tooltip>
    : typography;
}