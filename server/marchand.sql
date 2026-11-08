-- Base créée avant la version 1.73 : ajoute le marchand, sa map et sa case aux prix TTG (une seule fois).
ALTER TABLE merchant_prices ADD COLUMN merchant TEXT;
ALTER TABLE merchant_prices ADD COLUMN map INTEGER;
ALTER TABLE merchant_prices ADD COLUMN cell INTEGER;
