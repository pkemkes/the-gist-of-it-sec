import Card from '@mui/material/Card';
import { Gist } from '../types';
import { Box, Button, CardContent, SxProps, Typography } from '@mui/material';
import { ClickableTag } from './GistList/ClickableTag';
import { useNavigate } from 'react-router';
import { TextTag } from './GistInspector/TextTag';


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

const ToLocaleString = (isoTime: string) => (
  new Date(isoTime).toLocaleString("de-DE", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  })
);

export const GistCard = ({ gist, highlighted, similarity }: GistCardProps) => {
  const navigate = useNavigate();

  let dateString = ToLocaleString(gist.published);
  if (gist.published != gist.updated) {
    dateString += " — updated: " + ToLocaleString(gist.updated)
  }

  let feedTitle = gist.feed_title;
  if (gist.author) {
    feedTitle += " — " + gist.author;
  }

  let sxProps: SxProps = { 
    mb: '1rem',
  }

  if (highlighted) {
    sxProps = {
      ...sxProps,
      borderStyle: 'outset',
      borderWidth: '3px',
      borderColor: 'divider',
    }
  }

  const investigationUrl = `/?gist=${gist.id}`;

  const similarityNote = similarity == undefined 
    ? undefined 
    : <Box sx={{
        display: "flex",
        justifyContent: "right"
      }}>
        <Typography sx={{ 
          color: "text.secondary",
          mr: "0.5rem"
        }}>
          Similarity:
        </Typography>
        <Typography> 
          {Math.round(100 - (similarity*100))}%
        </Typography>
      </Box>;

  return (
    <Card elevation={ highlighted ? 8 : 3 } sx={sxProps}>
      <CardContent>
        <Box sx={{
          display: "grid",
          gridTemplateColumns: "auto 8rem",
        }}>
          <Typography>
            { feedTitle }
          </Typography>
          { similarityNote }
        </Box>
        <Typography variant='h5' component='div'>
          { gist.title }
        </Typography>
        <Typography sx={{ color: 'text.secondary', mb: 1.5 }}>
          { dateString }
        </Typography>
        <Typography variant='body2' sx={{ mb: 1.5 }}>
          { gist.summary }
        </Typography>
        { 
          similarity == undefined && highlighted == undefined
          ? ClickableTagList(gist.tags) 
          : TextTagList(gist.tags)
        }
        <Box
          sx={{
            // mx: "1rem",
            mt: "1rem",
            display: "grid",
            gridTemplateColumns: "50% auto",
            columnGap: "1rem",
          }}
        >
          <Button
            component="a"
            href={ gist.link }
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
            ? undefined
            : <Button
                onClick={ () => { 
                  navigate(investigationUrl);
                  document.documentElement.scrollTop = 0;
                }}
                variant="outlined"
                size="small"
              >
                Investigate further
              </Button>
          }
        </Box>
      </CardContent>
    </Card>
  )
};