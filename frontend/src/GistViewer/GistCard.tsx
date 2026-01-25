import Card from "@mui/material/Card";
import { FeedType, Gist } from "../types";
import { Box, Button, CardContent, SxProps, Typography } from "@mui/material";
import { ClickableTag } from "./GistList/ClickableTag";
import { useNavigate } from "react-router";
import { TextTag } from "./GistInspector/TextTag";
import { useAppSelector } from "../store";
import { selectTimezone } from "./slice";
import { ToLocaleString } from "./utils";


interface GistCardProps {
  gist: Gist,
  highlighted?: boolean,
  similarity?: number,
  key?: number
}

const ClickableTagList = (tagTexts: string[]) =>
  <div>
    {
      tagTexts.map((tagText: string, index: number) => <ClickableTag 
        tagText={ tagText }
        key={ index }
      />)
    }
  </div>

const TextTagList = (tagTexts: string[]) => 
  <Box
    sx={{
      display: "flex",
      flexWrap: "wrap",
      mt: "1rem"
    }}
  >
    { 
      tagTexts.map((tagText: string, index: number) => <TextTag
        tagText={ tagText }
        key={ index }
      />) 
    }
  </Box>;

export const GistCard = ({ gist, highlighted, similarity }: GistCardProps) => {
  const navigate = useNavigate();
  const timezone = useAppSelector(selectTimezone);

  let dateString = ToLocaleString(gist.published, timezone);
  if (gist.published != gist.updated) {
    dateString += " – updated: " + ToLocaleString(gist.updated, timezone)
  }

  let feedTitle = gist.feedTitle;
  if (gist.author) {
    feedTitle += " – " + gist.author;
  }

  let sxProps: SxProps = { 
    mb: "1rem",
  }

  if (highlighted) {
    sxProps = {
      ...sxProps,
      borderStyle: "outset",
      borderWidth: "3px",
      borderColor: "divider",
    }
  }

  const investigationUrl = `/?gist=${gist.id}`;

  const similarityNote = similarity == undefined 
    ? undefined 
    : <Box sx={{
        display: "flex",
        justifyContent: "right",
        justifySelf: "end",
        textAlign: "right",
        alignSelf: "start",
        mt: "calc(var(--first-line-height) / 2)",
        transform: "translateY(-50%)",
      }}>
        <Typography sx={{ 
          color: "text.secondary",
          mr: "0.25rem"
        }}>
          Similarity:
        </Typography>
        <Typography> 
          {Math.round(similarity*100)}%
        </Typography>
      </Box>;

  return (
    <Card elevation={ highlighted ? 8 : 3 } sx={sxProps}>
      <CardContent>
        <Box sx={{
          display: "grid",
          gridTemplateColumns: "1fr auto auto",
          columnGap: similarity ? "0.5rem" : undefined,
          alignItems: "start",
          "--first-line-height": "1.5rem",
        }}>
          <Typography sx={{ justifySelf: "start", textAlign: "left", lineHeight: "var(--first-line-height)" }}>
            { feedTitle }
          </Typography>
          { similarityNote }
          <Typography 
            color={gist.feedType == FeedType.News ? "primary" : "success"} 
            sx={{
              textTransform: "uppercase",
              fontWeight: "bold",
              fontSize: "0.875rem",
              justifySelf: "end",
              textAlign: "right",
              alignSelf: "start",
              mt: "calc(var(--first-line-height) / 2)",
              transform: "translateY(-50%)",
            }}
          >
            {FeedType[gist.feedType]}
          </Typography>
        </Box>
        <Typography variant="h5" component="div">
          { gist.title }
        </Typography>
        <Typography sx={{ color: "text.secondary" }}>
          { dateString }
        </Typography>
        { gist.isSponsoredContent ? (
          <Typography 
            sx={{ 
              color: "warning.main",
              fontSize: "0.7rem",
              my: 1,
              textTransform: "uppercase",
              border: "1px solid",
              borderColor: "divider",
              borderRadius: "4px",
              px: 1,
              py: 0.25,
              display: "inline-block",
            }}
          >
            Sponsored Content!
          </Typography>
        ) : null }
        <Typography variant="body2" sx={{ mb: 1.5 }}>
          { gist.summary }
        </Typography>
        { 
          similarity == undefined && highlighted == undefined
          ? ClickableTagList(gist.tags) 
          : TextTagList(gist.tags)
        }
        <Box
          sx={{
            mt: "1rem",
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            columnGap: "1rem",
          }}
        >
          <Button
            component="a"
            href={ gist.url }
            target="_blank"
            variant="outlined"
            size="small"
            sx={{
              textAlign: "center"
            }}
          >
            Read the full article
          </Button>
          { highlighted 
            ? <div />
            : <Button
                onClick={ () => { 
                  navigate(investigationUrl);
                  document.documentElement.scrollTop = 0;
                }}
                variant="outlined"
                size="small"
              >
                Find similar gists
              </Button>
          }
        </Box>
      </CardContent>
    </Card>
  )
};